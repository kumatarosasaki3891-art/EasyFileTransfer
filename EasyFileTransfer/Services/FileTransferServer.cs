using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyFileTransfer.Models;

namespace EasyFileTransfer.Services;

/// <summary>
/// スマホとPCのどちら向きにファイルを送るかを表すモードです。
/// </summary>
public enum TransferMode
{
    Upload,   // スマホからPCへアップロード（PC側で受信）
    Download  // PCからスマホへダウンロード（PC側から送信）
}

/// <summary>
/// スマホからのアクセスが正しいセッションかどうかを判定した結果です。
/// </summary>
public enum SessionValidationResult
{
    Valid,
    InvalidToken,
    Expired,
    IpMismatch
}

/// <summary>
/// 使い捨てセッション（1:1接続）の情報を入れておくクラスです。
/// 同じWi-Fi内に別の人がいても勝手にアクセスされないように、ランダムなトークンと接続元IPを覚えています。
/// </summary>
public sealed class TransferSession
{
    public string Token { get; }
    public TransferMode Mode { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public string? BoundClientIp { get; set; }
    public int UploadCount { get; set; }
    public int DownloadCount { get; set; }

    /// <summary>
    /// 新しいセッション情報を作ります（有効期限はとりあえず30分にしています）。
    /// </summary>
    public TransferSession(string token, TransferMode mode)
    {
        Token = token;
        Mode = mode;
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// PCで見つかったローカルIPアドレス候補と、優先度スコアをまとめるクラスです。
/// </summary>
public sealed record LocalIpCandidate(string IpAddress, string AdapterName, int Score)
{
    public string DisplayText => string.IsNullOrWhiteSpace(AdapterName)
        ? IpAddress
        : $"{IpAddress} ({AdapterName})";
}

/// <summary>
/// スマホとPCの間でファイルをやり取りするためのローカルWebサーバークラスです。
/// HttpListenerだとWindowsの管理者権限が必要になることがあったので、権限不要で動くTcpListenerを使って手作りしています。
/// </summary>
public sealed class FileTransferServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// バインドされたポート番号
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// サーバーが稼働中かどうか
    /// </summary>
    public bool IsRunning => _listener != null && !(_cts?.IsCancellationRequested ?? true);

    /// <summary>
    /// 検出されたローカルIPv4アドレス候補一覧（優先度順）
    /// </summary>
    public IReadOnlyList<LocalIpCandidate> IpCandidates { get; private set; } = [];

    /// <summary>
    /// 現在アクティブなセッション（使い捨てトークン）
    /// </summary>
    public TransferSession? CurrentSession { get; private set; }

    /// <summary>
    /// スマホ側がWeb画面を開いたときに発火します。
    /// </summary>
    public event Action? ClientConnected;

    /// <summary>
    /// スマホ側からファイルのアップロード受信が開始されたときに発火します。
    /// </summary>
    public event Action<string>? UploadStarted;

    /// <summary>
    /// ファイルの保存が完了したときに発火します。
    /// </summary>
    public event Action<ReceivedFileInfo>? FileReceived;

    /// <summary>
    /// スマホ側からファイルがダウンロードされたときに発火します。
    /// </summary>
    public event Action<string>? FileDownloaded;

    /// <summary>
    /// エラー発生時に発火します。
    /// </summary>
    public event Action<string>? ErrorOccurred;

    /// <summary>
    /// 新しい使い捨てトークンを発行して、現在のセッションを切り替えます。
    /// 別のスマホをつなぎ直したいときなどにも使います。
    /// </summary>
    public string CreateNewSession(TransferMode mode)
    {
        // ランダムな8桁の英数字トークン
        var tokenBytes = RandomNumberGenerator.GetBytes(4);
        var token = Convert.ToHexString(tokenBytes).ToLowerInvariant();
        CurrentSession = new TransferSession(token, mode);
        return token;
    }

    /// <summary>
    /// ローカルHTTPサーバーを起動します。
    /// デフォルトは5050番ポートを使い、使用中なら+10番まで順番に空きポートを探します。
    /// </summary>
    public void Start(int preferredPort = 5050)
    {
        if (IsRunning) return;

        IpCandidates = DetectLocalIpCandidates();
        if (IpCandidates.Count == 0)
        {
            throw new InvalidOperationException(LocalizationService.Instance.Get("NoNetworkError"));
        }

        if (CurrentSession == null)
        {
            CreateNewSession(TransferMode.Upload);
        }

        TcpListener? boundListener = null;
        int boundPort = 0;

        for (int p = preferredPort; p <= preferredPort + 10; p++)
        {
            try
            {
                var candidate = new TcpListener(IPAddress.Any, p);
                candidate.Start();
                boundListener = candidate;
                boundPort = p;
                break;
            }
            catch
            {
                // ポートが使用中の場合は次を試行
            }
        }

        if (boundListener == null)
        {
            boundListener = new TcpListener(IPAddress.Any, 0);
            boundListener.Start();
            boundPort = ((IPEndPoint)boundListener.LocalEndpoint).Port;
        }

        _listener = boundListener;
        Port = boundPort;
        _cts = new CancellationTokenSource();

        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>
    /// 稼働中のローカルサーバーを安全に停止します。
    /// </summary>
    public void Stop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        try
        {
            _listener?.Stop();
        }
        catch { }

        _listener = null;
        _cts = null;
    }

    /// <summary>
    /// スマホのカメラ（QRコード）に読み取らせるURLを組み立てます。
    /// モード・セッションID・言語コードをクエリパラメータに付けています。
    /// </summary>
    public string GetTransferUrl(string ipAddress, TransferMode mode)
    {
        var session = CurrentSession ?? new TransferSession(CreateNewSession(mode), mode);
        session.Mode = mode;

        var lang = LocalizationService.Instance.GetCurrentLanguageCode();
        var modeStr = mode == TransferMode.Download ? "download" : "upload";
        return $"http://{ipAddress}:{Port}/?mode={modeStr}&token={session.Token}&lang={Uri.EscapeDataString(lang)}";
    }

    /// <summary>
    /// スマホなどからのTCP接続をバックグラウンドで待ち受け続けるループです。
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException)
            {
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileTransferServer] Accept error: {ex.Message}");
                client?.Dispose();
            }
        }
    }

    /// <summary>
    /// 1件のHTTPリクエストを受け取って、画面の返却・アップロード保存・ダウンロード送信などを振り分けます。
    /// HTTPのヘッダー解析を自分で書いているので少し長くなっています。
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            client.ReceiveTimeout = 60000;
            client.SendTimeout = 60000;

            string clientIp = string.Empty;
            try
            {
                if (client.Client.RemoteEndPoint is IPEndPoint ep)
                {
                    clientIp = ep.Address.ToString();
                }
            }
            catch { }

            await using var stream = client.GetStream();

            // HTTPリクエストヘッダーの読み取り（最大 64KB まで）
            var headerBuffer = new List<byte>(4096);
            var singleByte = new byte[1];
            int matchState = 0;

            while (headerBuffer.Count < 65536)
            {
                int read = await stream.ReadAsync(singleByte.AsMemory(0, 1), token);
                if (read == 0) return;

                byte b = singleByte[0];
                headerBuffer.Add(b);

                if ((matchState == 0 || matchState == 2) && b == (byte)'\r') matchState++;
                else if ((matchState == 1 || matchState == 3) && b == (byte)'\n') matchState++;
                else if (b == (byte)'\r') matchState = 1;
                else matchState = 0;

                if (matchState == 4) break;
            }

            string headerText = Encoding.ASCII.GetString(headerBuffer.ToArray());
            var lines = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            var requestParts = lines[0].Split(' ');
            if (requestParts.Length < 2) return;

            string method = requestParts[0].ToUpperInvariant();
            string path = requestParts[1];

            long contentLength = 0;
            string? xFileName = null;
            string? headerToken = null;

            foreach (var line in lines.Skip(1))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var valPart = line["Content-Length:".Length..].Trim();
                    long.TryParse(valPart, out contentLength);
                }
                else if (line.StartsWith("X-Filename:", StringComparison.OrdinalIgnoreCase))
                {
                    var rawVal = line["X-Filename:".Length..].Trim();
                    try { xFileName = Uri.UnescapeDataString(rawVal); }
                    catch { xFileName = rawVal; }
                }
                else if (line.StartsWith("X-Session-Token:", StringComparison.OrdinalIgnoreCase))
                {
                    headerToken = line["X-Session-Token:".Length..].Trim();
                }
            }

            // CORS preflight
            if (method == "OPTIONS")
            {
                await WriteHttpResponseAsync(stream, "204 No Content", "text/plain", [], token);
                return;
            }

            // クエリパラメータの解析
            var queryParams = ParseQueryString(path);
            var reqToken = queryParams.GetValueOrDefault("token") ?? headerToken;
            var langCode = queryParams.GetValueOrDefault("lang") ?? LocalizationService.Instance.GetCurrentLanguageCode();

            // セッション検証
            var validation = ValidateSession(reqToken, clientIp);

            // ==========================================
            // 1. GET / または GET /?mode=...&token=...
            // ==========================================
            if (method == "GET" && (path == "/" || path.StartsWith("/?")))
            {
                try
                {
                    ClientConnected?.Invoke();
                }
                catch { }

                // セッションエラーの場合、エラーHTMLを表示
                if (validation != SessionValidationResult.Valid)
                {
                    var errorHtml = BuildSessionErrorHtml(validation, langCode);
                    var errBytes = Encoding.UTF8.GetBytes(errorHtml);
                    await WriteHttpResponseAsync(stream, "403 Forbidden", "text/html; charset=utf-8", errBytes, token);
                    return;
                }

                // 正常: モバイルWebページHTMLを返却
                var modeParam = queryParams.GetValueOrDefault("mode", "upload").ToLowerInvariant();
                var html = BuildMobileHtmlPage(langCode, modeParam, CurrentSession?.Token ?? string.Empty);
                var htmlBytes = Encoding.UTF8.GetBytes(html);
                await WriteHttpResponseAsync(stream, "200 OK", "text/html; charset=utf-8", htmlBytes, token);
                return;
            }

            // ==========================================
            // セッション検証 (API / Upload / Download)
            // ==========================================
            if (validation != SessionValidationResult.Valid)
            {
                string errMsg = validation switch
                {
                    SessionValidationResult.Expired => "Session expired",
                    SessionValidationResult.IpMismatch => "Session already bound to another client",
                    _ => "Invalid session token"
                };
                var errBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{errMsg}\",\"code\":\"{validation}\"}}");
                await WriteHttpResponseAsync(stream, "403 Forbidden", "application/json; charset=utf-8", errBytes, token);
                return;
            }

            // ==========================================
            // 2. GET /api/download-files (ダウンロード共有ファイル一覧)
            // ==========================================
            if (method == "GET" && path.StartsWith("/api/download-files", StringComparison.OrdinalIgnoreCase))
            {
                string downloadFolder = StorageSettings.GetDownloadFolder();
                var filesList = new List<SharedFileInfo>();

                if (Directory.Exists(downloadFolder))
                {
                    try
                    {
                        var di = new DirectoryInfo(downloadFolder);
                        foreach (var fi in di.GetFiles().OrderByDescending(f => f.LastWriteTime))
                        {
                            filesList.Add(new SharedFileInfo
                            {
                                FileName = fi.Name,
                                FullPath = fi.FullName,
                                FileSizeBytes = fi.Length,
                                LastModifiedAt = fi.LastWriteTime
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[FileTransferServer] List files error: {ex.Message}");
                    }
                }

                var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(filesList, jsonOptions);
                await WriteHttpResponseAsync(stream, "200 OK", "application/json; charset=utf-8", jsonBytes, token);
                return;
            }

            // ==========================================
            // 3. GET /download?file=... (スマホへのファイル送信)
            // ==========================================
            if (method == "GET" && path.StartsWith("/download", StringComparison.OrdinalIgnoreCase))
            {
                var requestedFileName = queryParams.GetValueOrDefault("file");
                if (string.IsNullOrWhiteSpace(requestedFileName))
                {
                    var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"File parameter is required\"}");
                    await WriteHttpResponseAsync(stream, "400 Bad Request", "application/json; charset=utf-8", errBytes, token);
                    return;
                }

                // パストラバーサル防止
                var safeFileName = Path.GetFileName(requestedFileName.Trim());
                string downloadFolder = StorageSettings.GetDownloadFolder();
                string targetFilePath = Path.Combine(downloadFolder, safeFileName);

                var fullTarget = Path.GetFullPath(targetFilePath);
                var fullFolder = Path.GetFullPath(downloadFolder);

                if (!fullTarget.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase) || !File.Exists(targetFilePath))
                {
                    var notFoundBytes = Encoding.UTF8.GetBytes("{\"error\":\"File not found\"}");
                    await WriteHttpResponseAsync(stream, "404 Not Found", "application/json; charset=utf-8", notFoundBytes, token);
                    return;
                }

                try
                {
                    var fileInfo = new FileInfo(targetFilePath);
                    long fileLength = fileInfo.Length;
                    string mimeType = GetMimeType(safeFileName);

                    string encodedName = Uri.EscapeDataString(safeFileName);
                    string header =
                        "HTTP/1.1 200 OK\r\n" +
                        $"Content-Type: {mimeType}\r\n" +
                        $"Content-Length: {fileLength}\r\n" +
                        $"Content-Disposition: attachment; filename=\"{safeFileName}\"; filename*=UTF-8''{encodedName}\r\n" +
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Cache-Control: no-cache, no-store\r\n" +
                        "Connection: close\r\n\r\n";

                    var headerBytes = Encoding.ASCII.GetBytes(header);
                    await stream.WriteAsync(headerBytes, token);

                    await using (var fileStream = new FileStream(targetFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                    {
                        var buffer = new byte[65536];
                        int bytesRead;
                        while ((bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
                        {
                            await stream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }

                    await stream.FlushAsync(token);

                    if (CurrentSession != null) CurrentSession.DownloadCount++;
                    FileDownloaded?.Invoke(safeFileName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileTransferServer] Download file error: {ex.Message}");
                    ErrorOccurred?.Invoke(ex.Message);
                }

                return;
            }

            // ==========================================
            // 4. POST /upload?name=... (スマホからのファイル受信)
            // ==========================================
            if (method == "POST" && path.StartsWith("/upload", StringComparison.OrdinalIgnoreCase))
            {
                // 最大 2GB 制限
                if (contentLength <= 0 || contentLength > 2L * 1024L * 1024L * 1024L)
                {
                    var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"Invalid file size\"}");
                    await WriteHttpResponseAsync(stream, "400 Bad Request", "application/json; charset=utf-8", errBytes, token);
                    return;
                }

                // ファイル名の抽出
                string fileName = ResolveFileName(path, xFileName);

                try
                {
                    UploadStarted?.Invoke(fileName);
                }
                catch { }

                string uploadFolder = StorageSettings.GetUploadFolder();
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string destinationPath = GetUniqueFilePath(uploadFolder, fileName);

                try
                {
                    await using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
                    {
                        var buffer = new byte[65536];
                        long remaining = contentLength;

                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min(remaining, buffer.Length);
                            int read = await stream.ReadAsync(buffer.AsMemory(0, toRead), token);
                            if (read == 0) break;

                            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
                            remaining -= read;
                        }

                        await fileStream.FlushAsync(token);
                    }

                    var fileInfo = new FileInfo(destinationPath);
                    var receivedInfo = new ReceivedFileInfo
                    {
                        FileName = Path.GetFileName(destinationPath),
                        FullPath = destinationPath,
                        FileSizeBytes = fileInfo.Length,
                        ReceivedAt = DateTime.Now
                    };

                    if (CurrentSession != null) CurrentSession.UploadCount++;

                    var okBytes = Encoding.UTF8.GetBytes($"{{\"status\":\"ok\",\"fileName\":\"{Uri.EscapeDataString(receivedInfo.FileName)}\"}}");
                    await WriteHttpResponseAsync(stream, "200 OK", "application/json; charset=utf-8", okBytes, token);

                    try
                    {
                        FileReceived?.Invoke(receivedInfo);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FileTransferServer] File write error: {ex.Message}");
                    ErrorOccurred?.Invoke(ex.Message);

                    var errBytes = Encoding.UTF8.GetBytes("{\"error\":\"Failed to save file\"}");
                    await WriteHttpResponseAsync(stream, "500 Internal Server Error", "application/json; charset=utf-8", errBytes, token);
                }

                return;
            }

            // その他（favicon.ico 等）
            await WriteHttpResponseAsync(stream, "404 Not Found", "text/plain", Encoding.ASCII.GetBytes("Not Found"), token);
        }
    }

    /// <summary>
    /// リクエストに付いてきたトークンとアクセス元IPが、今のセッションと一致するかチェックします。
    /// 最初に接続してきた端末のIPを記憶して、他の端末からの横入りを防いでいます。
    /// </summary>
    private SessionValidationResult ValidateSession(string? token, string clientIp)
    {
        if (CurrentSession == null || string.IsNullOrWhiteSpace(token))
        {
            return SessionValidationResult.InvalidToken;
        }

        if (!string.Equals(CurrentSession.Token, token.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return SessionValidationResult.InvalidToken;
        }

        if (CurrentSession.IsExpired)
        {
            return SessionValidationResult.Expired;
        }

        // 1:1 バインド: 初回接続クライアントのIPを記録
        if (string.IsNullOrEmpty(CurrentSession.BoundClientIp))
        {
            if (!string.IsNullOrEmpty(clientIp))
            {
                CurrentSession.BoundClientIp = clientIp;
            }
        }
        else if (!string.IsNullOrEmpty(clientIp) && !string.Equals(CurrentSession.BoundClientIp, clientIp, StringComparison.OrdinalIgnoreCase))
        {
            // 別のIPからのアクセス
            return SessionValidationResult.IpMismatch;
        }

        return SessionValidationResult.Valid;
    }

    /// <summary>
    /// URLの「?」以降にあるクエリパラメータ（token=xxx など）を分解してDictionaryにします。
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var qIndex = path.IndexOf('?');
        if (qIndex >= 0 && qIndex < path.Length - 1)
        {
            var queryString = path[(qIndex + 1)..];
            var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2);
                if (kv.Length == 2)
                {
                    try { result[kv[0]] = Uri.UnescapeDataString(kv[1]); }
                    catch { result[kv[0]] = kv[1]; }
                }
                else if (kv.Length == 1)
                {
                    result[kv[0]] = string.Empty;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// リクエストヘッダーやURLパラメータから保存するファイル名を取り出します。
    /// もしファイル名が取れなかったときは日時入りの仮名を作ります。
    /// </summary>
    private static string ResolveFileName(string path, string? headerFileName)
    {
        if (!string.IsNullOrWhiteSpace(headerFileName))
        {
            var cleaned = Path.GetFileName(headerFileName.Trim());
            if (!string.IsNullOrWhiteSpace(cleaned)) return cleaned;
        }

        var queries = ParseQueryString(path);
        if (queries.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n))
        {
            return Path.GetFileName(n.Trim());
        }
        if (queries.TryGetValue("filename", out var fn) && !string.IsNullOrWhiteSpace(fn))
        {
            return Path.GetFileName(fn.Trim());
        }

        return $"transferred_file_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
    }

    /// <summary>
    /// 保存先フォルダーに同じ名前のファイルがあった場合、上書きしないように「(1)」「(2)」を付けて別名にします。
    /// ファイル名に使えない文字もここで取り除いています。
    /// </summary>
    private static string GetUniqueFilePath(string folder, string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fileName.Where(ch => !invalidChars.Contains(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = $"file_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
        }

        string fullPath = Path.Combine(folder, safeName);
        if (!File.Exists(fullPath)) return fullPath;

        string nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        string ext = Path.GetExtension(safeName);

        int counter = 1;
        while (true)
        {
            string candidate = Path.Combine(folder, $"{nameWithoutExt} ({counter}){ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    /// <summary>
    /// 拡張子からHTTPレスポンスに付ける Content-Type（MIMEタイプ）を判定します。
    /// </summary>
    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".heic" => "image/heic",
            ".pdf" => "application/pdf",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// HTTPレスポンスのヘッダーと本文（バイト配列）をソケットへ書き込んで返します。
    /// </summary>
    private static async Task WriteHttpResponseAsync(
        NetworkStream stream,
        string status,
        string contentType,
        byte[] body,
        CancellationToken token)
    {
        string header =
            $"HTTP/1.1 {status}\r\n" +
            $"Content-Type: {contentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: Content-Type, X-Filename, X-Session-Token\r\n" +
            "Cache-Control: no-store, no-cache\r\n" +
            "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, token);
        if (body.Length > 0)
        {
            await stream.WriteAsync(body, token);
        }
        await stream.FlushAsync(token);
    }

    /// <summary>
    /// PCのネットワークアダプターを調べて、スマホから繋がりそうなローカルIPv4アドレスを探します。
    /// WSLやDockerなどの仮想アダプターは繋がらないことが多いので、スコアを下げて後ろに回しています。
    /// </summary>
    public static List<LocalIpCandidate> DetectLocalIpCandidates()
    {
        var list = new List<LocalIpCandidate>();
        string[] virtualKeywords =
        [
            "WSL", "Hyper-V", "vEthernet", "Virtual", "VMware", "VirtualBox",
            "Docker", "Loopback", "VPN", "Tailscale", "ZeroTier", "Hamachi",
            "TAP-", "TUN", "Bluetooth", "NPcap"
        ];

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                string nameAndDesc = $"{ni.Name} {ni.Description}";
                bool isVirtual = virtualKeywords.Any(k => nameAndDesc.Contains(k, StringComparison.OrdinalIgnoreCase));

                var ipProps = ni.GetIPProperties();
                bool hasGateway = ipProps.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(g.Address) &&
                    !g.Address.Equals(IPAddress.Any));

                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;

                    string ipStr = ua.Address.ToString();
                    if (ipStr.StartsWith("169.254.", StringComparison.Ordinal)) continue; // APIPA除外

                    int score = 0;
                    if (!isVirtual) score += 100;
                    if (hasGateway) score += 50;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 30;
                    else if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 20;

                    if (ipStr.StartsWith("192.168.", StringComparison.Ordinal)) score += 15;
                    else if (ipStr.StartsWith("10.", StringComparison.Ordinal)) score += 10;
                    else if (ipStr.StartsWith("172.", StringComparison.Ordinal)) score += 5;

                    list.Add(new LocalIpCandidate(ipStr, ni.Name, score));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileTransferServer] DetectLocalIpCandidates error: {ex.Message}");
        }

        return list
            .OrderByDescending(c => c.Score)
            .GroupBy(c => c.IpAddress)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// スマホのブラウザに表示するHTMLページを生成します（テンプレート内のプレースホルダーを置換します）。
    /// </summary>
    private static string BuildMobileHtmlPage(string langCode, string initialMode, string sessionToken)
    {
        var code = (langCode ?? LocalizationService.Instance.GetCurrentLanguageCode()).Trim().ToLowerInvariant();
        var mode = initialMode == "download" ? "download" : "upload";

        return MobileHtmlTemplate
            .Replace("DEFAULT_LANG_CODE_PLACEHOLDER", code, StringComparison.Ordinal)
            .Replace("DEFAULT_MODE_PLACEHOLDER", mode, StringComparison.Ordinal)
            .Replace("SESSION_TOKEN_PLACEHOLDER", sessionToken, StringComparison.Ordinal);
    }

    /// <summary>
    /// セッション切れや他端末からのアクセス時にスマホ画面へ出すエラーページHTMLを作ります。
    /// </summary>
    private static string BuildSessionErrorHtml(SessionValidationResult result, string langCode)
    {
        string title;
        string message;
        string hint;

        var code = (langCode ?? "ja").Trim().ToLowerInvariant();

        if (result == SessionValidationResult.IpMismatch)
        {
            title = code switch
            {
                "en" => "⚠️ Access Denied (1:1 Protection)",
                "zh" => "⚠️ 访问受限（1:1 保护）",
                "ko" => "⚠️ 접근 차단됨 (1:1 보호)",
                "zh-tw" => "⚠️ 存取受限（1:1 保護）",
                _ => "⚠️ 接続が制限されています（1:1 保護）"
            };
            message = code switch
            {
                "en" => "This transfer session is already active with another device.",
                "zh" => "当前传输会话已与另一台设备建立连接。",
                "ko" => "현재 전송 세션이 이미 다른 기기에 연결되어 있습니다.",
                "zh-tw" => "目前的傳輸階段已與另一部裝置建立連線。",
                _ => "このセッションは既に別の端末と1:1接続されています。"
            };
            hint = code switch
            {
                "en" => "Please refresh the QR code on your PC to connect this device.",
                "zh" => "请在电脑端点击“刷新二维码”以连接此设备。",
                "ko" => "이 기기를 연결하려면 PC에서 'QR 갱신'을 눌러주세요.",
                "zh-tw" => "請在電腦端點選「重新整理 QR」以連接此裝置。",
                _ => "この端末を接続したい場合は、PC画面の「🔄 QR更新」をクリックして新しいQRコードをスキャンしてください。"
            };
        }
        else
        {
            title = code switch
            {
                "en" => "⚠️ Session Invalid or Expired",
                "zh" => "⚠️ 会话无效或已过期",
                "ko" => "⚠️ 세션이 유효하지 않거나 만료되었습니다",
                "zh-tw" => "⚠️ 連線階段無效或已過期",
                _ => "⚠️ セッションが無効または有効期限切れです"
            };
            message = code switch
            {
                "en" => "The one-time session token has expired or is no longer valid.",
                "zh" => "一次性会话令牌已过期或无效。",
                "ko" => "일회용 세션 토큰이 만료되었거나 유효하지 않습니다.",
                "zh-tw" => "一次性階段權杖已過期或無效。",
                _ => "使い捨てセッションの有効期限が切れたか、無効になりました。"
            };
            hint = code switch
            {
                "en" => "Please scan the latest QR code displayed on your PC screen.",
                "zh" => "请扫描电脑屏幕上显示的最新二维码。",
                "ko" => "PC 화면에 표시된 최신 QR 코드를 다시 스캔해 주세요.",
                "zh-tw" => "請掃描電腦螢幕上顯示的最新 QR Code。",
                _ => "PC画面に表示されている最新のQRコードを再度カメラでスキャンしてください。"
            };
        }

        return $$"""
<!DOCTYPE html>
<html lang="{{code}}">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
<title>{{title}}</title>
<style>
  body {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    background: #F8FAFC;
    color: #1F2937;
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 100vh;
    padding: 24px;
    margin: 0;
    box-sizing: border-box;
  }
  .card {
    background: #FFFFFF;
    border: 1px solid #E2E8F0;
    border-radius: 20px;
    padding: 32px 24px;
    max-width: 420px;
    text-align: center;
    box-shadow: 0 10px 25px rgba(0,0,0,0.06);
  }
  .icon { font-size: 48px; margin-bottom: 16px; }
  h1 { font-size: 20px; margin-bottom: 12px; color: #DC2626; }
  p { font-size: 14px; line-height: 1.6; color: #4B5563; margin-bottom: 12px; }
  .hint { font-size: 12.5px; color: #6B7280; background: #F3F4F6; padding: 12px; border-radius: 10px; }
</style>
</head>
<body>
  <div class="card">
    <div class="icon">🔒</div>
    <h1>{{title}}</h1>
    <p>{{message}}</p>
    <div class="hint">{{hint}}</div>
  </div>
</body>
</html>
""";
    }

    /// <summary>
    /// リソースの後片付け（サーバーの停止とCancellationTokenSourceの破棄）を行います。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _cts?.Dispose();
    }

    #region Mobile Web App HTML Template

    private const string MobileHtmlTemplate = """
<!DOCTYPE html>
<html lang="ja">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
<title>EasyFileTransfer</title>
<style>
  :root {
    --primary: #4F46E5;
    --primary-hover: #4338CA;
    --primary-light: #EEF2FF;
    --success: #10B981;
    --success-light: #ECFDF5;
    --error: #EF4444;
    --error-light: #FEF2F2;
    --text-main: #1F2937;
    --text-sub: #6B7280;
    --bg-page: #F8FAFC;
    --card-bg: #FFFFFF;
    --border: #E2E8F0;
    --border-accent: #C7D2FE;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --primary: #6366F1;
      --primary-hover: #4F46E5;
      --primary-light: #1E1B4B;
      --success: #34D399;
      --success-light: #064E3B;
      --error: #F87171;
      --error-light: #450A0A;
      --text-main: #F9FAFB;
      --text-sub: #9CA3AF;
      --bg-page: #0F172A;
      --card-bg: #1E293B;
      --border: #334155;
      --border-accent: #3730A3;
    }
  }

  * { box-sizing: border-box; margin: 0; padding: 0; -webkit-tap-highlight-color: transparent; }
  body {
    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Hiragino Sans", "Noto Sans JP", sans-serif;
    background: var(--bg-page);
    color: var(--text-main);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: flex-start;
    min-height: 100vh;
    padding: 20px 16px 40px;
  }
  .container {
    width: 100%;
    max-width: 480px;
    display: flex;
    flex-direction: column;
    gap: 16px;
  }
  .card {
    background: var(--card-bg);
    border: 1px solid var(--border);
    border-radius: 20px;
    padding: 24px 20px;
    box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.05), 0 8px 10px -6px rgba(0, 0, 0, 0.01);
    text-align: center;
  }
  .top-bar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
  }
  .badge-wifi {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    background: var(--success-light);
    color: var(--success);
    font-size: 11.5px;
    font-weight: 700;
    padding: 4px 10px;
    border-radius: 999px;
  }
  .lang-select {
    background: var(--primary-light);
    color: var(--primary);
    border: 1px solid var(--border-accent);
    border-radius: 8px;
    padding: 4px 8px;
    font-size: 12px;
    font-weight: 600;
    outline: none;
    cursor: pointer;
  }

  /* モード切り替えタブ */
  .mode-tabs {
    display: flex;
    background: var(--bg-page);
    border: 1px solid var(--border);
    border-radius: 12px;
    padding: 4px;
    margin-bottom: 18px;
    gap: 4px;
  }
  .mode-tab-btn {
    flex: 1;
    border: none;
    background: transparent;
    color: var(--text-sub);
    font-size: 13.5px;
    font-weight: 700;
    padding: 10px 8px;
    border-radius: 8px;
    cursor: pointer;
    transition: all 0.15s ease;
  }
  .mode-tab-btn.active {
    background: var(--card-bg);
    color: var(--primary);
    box-shadow: 0 2px 6px rgba(0,0,0,0.08);
  }

  .app-tag {
    display: inline-block;
    background: var(--primary-light);
    color: var(--primary);
    font-size: 12px;
    font-weight: 700;
    padding: 3px 12px;
    border-radius: 999px;
    margin-bottom: 8px;
  }
  h1 {
    font-size: 21px;
    font-weight: 800;
    margin-bottom: 6px;
    letter-spacing: -0.02em;
  }
  p.desc {
    font-size: 13.5px;
    color: var(--text-sub);
    line-height: 1.5;
    margin-bottom: 14px;
  }
  .security-note {
    background: var(--primary-light);
    border: 1px dashed var(--border-accent);
    border-radius: 12px;
    padding: 8px 12px;
    font-size: 11.5px;
    color: var(--primary);
    line-height: 1.45;
    margin-bottom: 18px;
    text-align: center;
  }

  /* アップロードエリア */
  .format-tags {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 6px;
    margin-bottom: 20px;
  }
  .format-chip {
    background: rgba(148, 163, 184, 0.12);
    border: 1px solid var(--border);
    color: var(--text-sub);
    font-size: 11px;
    font-weight: 600;
    padding: 3px 8px;
    border-radius: 6px;
  }
  .btn-upload-file {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 10px;
    width: 100%;
    background: var(--primary);
    color: #FFFFFF;
    font-size: 16.5px;
    font-weight: 700;
    padding: 15px 14px;
    border-radius: 14px;
    border: none;
    cursor: pointer;
    box-shadow: 0 4px 14px rgba(79, 70, 229, 0.35);
    margin-bottom: 12px;
    transition: transform 0.1s ease, background-color 0.15s ease;
  }
  .btn-upload-file:active {
    background: var(--primary-hover);
    transform: scale(0.98);
  }
  .btn-upload-photo {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    width: 100%;
    background: var(--primary-light);
    color: var(--primary);
    font-size: 14.5px;
    font-weight: 700;
    padding: 13px 14px;
    border-radius: 12px;
    border: 1px solid var(--border-accent);
    cursor: pointer;
    margin-bottom: 12px;
  }
  .btn-upload-photo:active {
    filter: brightness(0.95);
  }
  input[type="file"] { display: none; }

  /* ドロップゾーン */
  .drop-zone {
    border: 2px dashed var(--border-accent);
    background: rgba(99, 102, 241, 0.03);
    border-radius: 14px;
    padding: 16px;
    font-size: 12.5px;
    color: var(--text-sub);
    margin-bottom: 16px;
    transition: background 0.2s;
  }
  .drop-zone.dragover {
    background: rgba(99, 102, 241, 0.12);
    border-color: var(--primary);
  }

  /* 選択ファイルリスト */
  #selectedListContainer {
    display: none;
    text-align: left;
    margin-top: 14px;
  }
  .selected-header {
    font-size: 13px;
    font-weight: 700;
    color: var(--text-main);
    margin-bottom: 8px;
  }
  .file-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 8px 12px;
    background: var(--bg-page);
    border: 1px solid var(--border);
    border-radius: 8px;
    margin-bottom: 6px;
    font-size: 13px;
  }
  .file-info {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    padding-right: 8px;
  }
  .file-name {
    font-weight: 600;
    color: var(--text-main);
  }
  .file-size {
    font-size: 11px;
    color: var(--text-sub);
  }

  /* プログレスバー */
  #progressContainer {
    display: none;
    margin-top: 16px;
  }
  .progress-bar-bg {
    width: 100%;
    height: 10px;
    background: var(--border);
    border-radius: 999px;
    overflow: hidden;
  }
  .progress-bar-fill {
    height: 100%;
    width: 0%;
    background: var(--primary);
    border-radius: 999px;
    transition: width 0.15s ease;
  }
  .progress-status {
    margin-top: 8px;
    font-size: 13px;
    font-weight: 700;
    color: var(--primary);
  }

  /* 結果表示 */
  #resultBox {
    display: none;
    margin-top: 18px;
    padding: 16px;
    border-radius: 14px;
    font-size: 14px;
    font-weight: 700;
    line-height: 1.5;
  }
  .result-ok {
    background: var(--success-light);
    color: var(--success);
    border: 1px solid var(--success);
  }
  .result-err {
    background: var(--error-light);
    color: var(--error);
    border: 1px solid var(--error);
  }
  .btn-reset {
    margin-top: 14px;
    background: var(--primary);
    color: #FFFFFF;
    font-size: 14px;
    font-weight: 700;
    padding: 11px 20px;
    border-radius: 10px;
    border: none;
    cursor: pointer;
    display: inline-block;
  }

  /* ダウンロードエリア */
  #downloadArea {
    display: none;
    text-align: left;
  }
  .download-toolbar {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
  }
  .btn-refresh-files {
    background: var(--bg-page);
    border: 1px solid var(--border);
    color: var(--text-main);
    font-size: 12px;
    font-weight: 600;
    padding: 6px 12px;
    border-radius: 8px;
    cursor: pointer;
  }
  .download-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  .download-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: var(--bg-page);
    border: 1px solid var(--border);
    border-radius: 12px;
    padding: 12px 14px;
    transition: transform 0.1s ease;
  }
  .download-item:active {
    transform: scale(0.99);
  }
  .item-left {
    display: flex;
    align-items: center;
    gap: 12px;
    overflow: hidden;
  }
  .item-icon {
    font-size: 24px;
    flex-shrink: 0;
  }
  .item-details {
    overflow: hidden;
  }
  .item-title {
    font-size: 13.5px;
    font-weight: 700;
    color: var(--text-main);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .item-meta {
    font-size: 11px;
    color: var(--text-sub);
    margin-top: 2px;
  }
  .btn-item-download {
    flex-shrink: 0;
    background: var(--primary);
    color: #FFFFFF;
    border: none;
    border-radius: 8px;
    font-size: 12px;
    font-weight: 700;
    padding: 8px 14px;
    cursor: pointer;
    text-decoration: none;
    display: inline-flex;
    align-items: center;
    gap: 4px;
  }
  .empty-download-state {
    text-align: center;
    padding: 32px 16px;
    color: var(--text-sub);
    font-size: 13px;
    line-height: 1.6;
    background: var(--bg-page);
    border: 1px dashed var(--border);
    border-radius: 12px;
  }
</style>
</head>
<body>
  <div class="container">
    <div class="card">
      <div class="top-bar">
        <div class="badge-wifi" id="lblWifiBadge">🟢 PCと直接通信中（1:1保護）</div>
        <select class="lang-select" id="langSelector" onchange="changeLanguage(this.value)">
          <option value="ja">🇯🇵 日本語</option>
          <option value="en">🇺🇸 English</option>
          <option value="zh">🇨🇳 简体中文</option>
          <option value="ko">🇰🇷 한국어</option>
          <option value="zh-tw">🇹🇼 繁體中文</option>
          <option value="vi">🇻🇳 Tiếng Việt</option>
          <option value="es">🇪🇸 Español</option>
          <option value="de">🇩🇪 Deutsch</option>
          <option value="fr">🇫🇷 Français</option>
          <option value="pt">🇵🇹 Português</option>
          <option value="it">🇮🇹 Italiano</option>
        </select>
      </div>

      <!-- モード切り替えタブ -->
      <div class="mode-tabs">
        <button class="mode-tab-btn active" id="tabUploadBtn" onclick="switchMode('upload')">
          <span id="lblTabUpload">📤 PCへ送る</span>
        </button>
        <button class="mode-tab-btn" id="tabDownloadBtn" onclick="switchMode('download')">
          <span id="lblTabDownload">📥 PCから受ける</span>
        </button>
      </div>

      <div class="app-tag">⚡ EasyFileTransfer</div>
      <h1 id="lblTitle">📱 PCへファイルを転送</h1>
      <p class="desc" id="lblDesc">ファイルまたは写真を選択すると、PCの指定フォルダーへ直接転送されます。</p>

      <div class="security-note" id="lblSecurity">
        🔒 ※ インターネットを経由せず、同一Wi-Fi内だけで安全・高速に直接転送されます。
      </div>

      <!-- ============================================== -->
      <!-- 1. アップロードエリア (スマホ → PC) -->
      <!-- ============================================== -->
      <div id="uploadArea">
        <div class="format-tags">
          <span class="format-chip">🖼️ 画像 (JPEG/PNG/HEIC)</span>
          <span class="format-chip">📕 PDF</span>
          <span class="format-chip">📊 CSV</span>
          <span class="format-chip">⚙️ JSON</span>
          <span class="format-chip">🗺️ GPX</span>
          <span class="format-chip">📦 ZIP</span>
          <span class="format-chip">📄 その他全般</span>
        </div>

        <div id="actionArea">
          <label class="btn-upload-file" for="fileInput">
            <span id="lblSelectFiles">📄 ファイルを選択 (写真・PDF・Zip等)</span>
          </label>
          <input id="fileInput" type="file" multiple />

          <label class="btn-upload-photo" for="cameraInput">
            <span id="lblCapture">📷 カメラで撮影して送信</span>
          </label>
          <input id="cameraInput" type="file" accept="image/*" capture="environment" />

          <div class="drop-zone" id="dropZone">
            <span id="lblDropHint">またはここにファイルをドラッグ＆ドロップ</span>
          </div>
        </div>

        <div id="selectedListContainer">
          <div class="selected-header" id="lblSelectedCount">選択されたファイル:</div>
          <div id="fileItemsList"></div>
          <button class="btn-upload-file" id="btnUploadNow" onclick="startUpload()" style="margin-top: 12px;">
            <span id="lblStartUpload">🚀 PCへ転送開始</span>
          </button>
        </div>

        <div id="progressContainer">
          <div class="progress-bar-bg">
            <div class="progress-bar-fill" id="progressBar"></div>
          </div>
          <div class="progress-status" id="progressStatus">PCへ転送中... (0%)</div>
        </div>

        <div id="resultBox"></div>
      </div>

      <!-- ============================================== -->
      <!-- 2. ダウンロードエリア (PC → スマホ) -->
      <!-- ============================================== -->
      <div id="downloadArea">
        <div class="download-toolbar">
          <span style="font-size: 13px; font-weight: 700; color: var(--text-main);" id="lblPcFilesHeader">PC上の共有ファイル:</span>
          <button class="btn-refresh-files" onclick="fetchDownloadFiles()">
            <span id="lblRefreshFiles">🔄 一覧更新</span>
          </button>
        </div>

        <div id="downloadFilesContainer" class="download-list">
          <!-- JSで動的生成 -->
        </div>
      </div>

    </div>
  </div>

<script>
  const SESSION_TOKEN = "SESSION_TOKEN_PLACEHOLDER";
  let currentMode = "DEFAULT_MODE_PLACEHOLDER" || "upload";
  let currentLang = "DEFAULT_LANG_CODE_PLACEHOLDER" || "ja";
  let pendingFiles = [];

  const I18N = {
    ja: {
      wifi: "🟢 PCと直接通信中（1:1保護）",
      tabUpload: "📤 PCへ送る",
      tabDownload: "📥 PCから受ける",
      titleUpload: "📱 PCへファイルを転送",
      descUpload: "ファイルまたは写真を選択すると、PCの指定フォルダーへ直接転送されます。",
      titleDownload: "📥 PCからファイルをダウンロード",
      descDownload: "PC上の共有ファイル一覧です。タップしてスマホに保存できます。",
      security: "🔒 ※ インターネットを経由せず、同一Wi-Fi内だけで安全・高速に直接転送されます。",
      selectFiles: "📄 ファイルを選択 (画像・PDF・Zip等)",
      capture: "📷 カメラで撮影して送信",
      dropHint: "またはここにファイルをドラッグ＆ドロップ",
      selectedCount: "選択されたファイル: {0} 件",
      startUpload: "🚀 PCへ転送開始 ({0} 件)",
      transferring: "🔄 PCへ転送中... ({0}%)",
      success: "✅ PCへの転送が完了しました！<br/>フォルダーに安全に保存されました。",
      sendMore: "➕ 続けて別のファイルを送る",
      fail: "⚠️ 転送に失敗しました（ステータス: {0}）。もう一度お試しください。",
      netErr: "⚠️ 通信エラーが発生しました: ",
      pcFilesHeader: "PC上の共有ファイル:",
      refreshFiles: "🔄 一覧更新",
      btnDownload: "⬇️ ダウンロード",
      emptyDownload: "📂 PCの共有フォルダーにファイルがありません。<br/>PCアプリから「＋ファイルを追加」してください。",
      loading: "読み込み中..."
    },
    en: {
      wifi: "🟢 Direct PC Connection (1:1 Protected)",
      tabUpload: "📤 Send to PC",
      tabDownload: "📥 Receive from PC",
      titleUpload: "📱 Transfer Files to PC",
      descUpload: "Select files or photos below to transfer directly to your designated PC folder.",
      titleDownload: "📥 Download Files from PC",
      descDownload: "Tap files below to download directly to your phone from PC.",
      security: "🔒 * Transferred safely and quickly over local Wi-Fi without going through the internet.",
      selectFiles: "📄 Select Files (Photos, PDF, Zip, etc.)",
      capture: "📷 Take Photo & Send",
      dropHint: "or drag & drop files here",
      selectedCount: "Selected files: {0}",
      startUpload: "🚀 Start Transfer ({0})",
      transferring: "🔄 Transferring to PC... ({0}%)",
      success: "✅ Transfer completed successfully!<br/>Files saved safely to your PC folder.",
      sendMore: "➕ Send More Files",
      fail: "⚠️ Transfer failed (Status: {0}). Please try again.",
      netErr: "⚠️ Network error: ",
      pcFilesHeader: "Files shared from PC:",
      refreshFiles: "🔄 Refresh",
      btnDownload: "⬇️ Download",
      emptyDownload: "📂 No files in PC shared folder.<br/>Please add files from the PC application.",
      loading: "Loading..."
    },
    zh: {
      wifi: "🟢 与电脑直接通信中（1:1保护）",
      tabUpload: "📤 发送到电脑",
      tabDownload: "📥 从电脑接收",
      titleUpload: "📱 传输文件到电脑",
      descUpload: "从下方选择文件或照片，即可直接传输到电脑的指定文件夹。",
      titleDownload: "📥 从电脑下载文件",
      descDownload: "以下是电脑共享的文件列表，点击即可下载到手机。",
      security: "🔒 ※ 无需通过互联网，仅在本地 Wi-Fi 内安全、高速直接传输。",
      selectFiles: "📄 选择文件（图片、PDF、Zip等）",
      capture: "📷 拍照并发送",
      dropHint: "或将文件拖放到此处",
      selectedCount: "已选择文件: {0} 个",
      startUpload: "🚀 开始传输到电脑 ({0} 个)",
      transferring: "🔄 正在传输到电脑... ({0}%)",
      success: "✅ 已成功传输到电脑！<br/>文件已安全保存到指定文件夹。",
      sendMore: "➕ 继续发送其他文件",
      fail: "⚠️ 传输失败（状态: {0}）。请重试。",
      netErr: "⚠️ 发生通信错误: ",
      pcFilesHeader: "电脑共享的文件:",
      refreshFiles: "🔄 刷新",
      btnDownload: "⬇️ 下载",
      emptyDownload: "📂 电脑共享文件夹中暂无文件。<br/>请在电脑端添加文件。",
      loading: "加载中..."
    },
    ko: {
      wifi: "🟢 PC와 직접 통신 중 (1:1 보호)",
      tabUpload: "📤 PC로 보내기",
      tabDownload: "📥 PC에서 받기",
      titleUpload: "📱 PC로 파일 전송",
      descUpload: "아래에서 파일이나 사진을 선택하면 PC의 지정된 폴더로 직접 전송됩니다.",
      titleDownload: "📥 PC에서 파일 다운로드",
      descDownload: "PC에서 공유 중인 파일 목록입니다. 탭하여 스마트폰에 저장하세요.",
      security: "🔒 ※ 인터넷을 거치지 않고 로컬 Wi-Fi 내에서만 안전하고 빠르게 직접 전송됩니다.",
      selectFiles: "📄 파일 선택 (사진, PDF, Zip 등)",
      capture: "📷 카메라 촬영 후 전송",
      dropHint: "또는 여기에 파일을 드래그 앤 드롭",
      selectedCount: "선택된 파일: {0}개",
      startUpload: "🚀 PC로 전송 시작 ({0}개)",
      transferring: "🔄 PC로 전송 중... ({0}%)",
      success: "✅ PC로 전송이 완료되었습니다!<br/>폴더에 안전하게 저장되었습니다.",
      sendMore: "➕ 추가 파일 전송하기",
      fail: "⚠️ 전송에 실패했습니다 (상태: {0}). 다시 시도해 주세요.",
      netErr: "⚠️ 통신 오류가 발생했습니다: ",
      pcFilesHeader: "PC 공유 파일 목록:",
      refreshFiles: "🔄 새로고침",
      btnDownload: "⬇️ 다운로드",
      emptyDownload: "📂 PC 공유 폴더에 파일이 없습니다.<br/>PC 앱에서 파일을 추가해 주세요.",
      loading: "로딩 중..."
    },
    "zh-tw": {
      wifi: "🟢 與電腦直接通訊中（1:1保護）",
      tabUpload: "📤 傳送至電腦",
      tabDownload: "📥 從電腦接收",
      titleUpload: "📱 傳輸檔案至電腦",
      descUpload: "從下方選取檔案或相片，即可直接傳輸至電腦的指定資料夾。",
      titleDownload: "📥 從電腦下載檔案",
      descDownload: "以下是電腦分享的檔案清單，輕按即可下載至手機。",
      security: "🔒 ※ 無須透過網際網路，僅在區域 Wi-Fi 內安全、高速直接傳輸。",
      selectFiles: "📄 選取檔案（圖片、PDF、Zip等）",
      capture: "📷 拍照並傳送",
      dropHint: "或將檔案拖放至此處",
      selectedCount: "已選取檔案: {0} 個",
      startUpload: "🚀 開始傳輸至電腦 ({0} 個)",
      transferring: "🔄 正在傳輸至電腦... ({0}%)",
      success: "✅ 已成功傳輸至電腦！<br/>檔案已安全儲存至指定資料夾。",
      sendMore: "➕ 繼續傳送其他檔案",
      fail: "⚠️ 傳送失敗（狀態: {0}）。請重試。",
      netErr: "⚠️ 發生通訊錯誤: ",
      pcFilesHeader: "電腦分享的檔案:",
      refreshFiles: "🔄 重新整理",
      btnDownload: "⬇️ 下載",
      emptyDownload: "📂 電腦分享資料夾中尚無檔案。<br/>請在電腦端新增檔案。",
      loading: "載入中..."
    },
    vi: {
      wifi: "🟢 Giao tiếp trực tiếp với PC (Bảo vệ 1:1)",
      tabUpload: "📤 Gửi sang PC",
      tabDownload: "📥 Nhận từ PC",
      titleUpload: "📱 Truyền tệp sang PC",
      descUpload: "Chọn tệp hoặc ảnh bên dưới để chuyển trực tiếp vào thư mục PC.",
      titleDownload: "📥 Tải tệp từ PC",
      descDownload: "Danh sách tệp chia sẻ từ PC. Nhấn để tải về điện thoại.",
      security: "🔒 * Truyền trực tiếp và an toàn trong mạng Wi-Fi nội bộ mà không qua internet.",
      selectFiles: "📄 Chọn tệp (Ảnh, PDF, Zip...)",
      capture: "📷 Chụp ảnh và gửi",
      dropHint: "hoặc kéo và thả tệp vào đây",
      selectedCount: "Tệp đã chọn: {0}",
      startUpload: "🚀 Bắt đầu chuyển ({0})",
      transferring: "🔄 Đang truyền sang PC... ({0}%)",
      success: "✅ Đã truyền thành công sang PC!<br/>Tệp đã được lưu an toàn.",
      sendMore: "➕ Gửi thêm tệp khác",
      fail: "⚠️ Truyền thất bại (Trạng thái: {0}). Vui lòng thử lại.",
      netErr: "⚠️ Đã xảy ra lỗi giao tiếp: ",
      pcFilesHeader: "Tệp chia sẻ từ PC:",
      refreshFiles: "🔄 Làm mới",
      btnDownload: "⬇️ Tải về",
      emptyDownload: "📂 Không có tệp nào trong thư mục chia sẻ PC.<br/>Vui lòng thêm tệp từ ứng dụng PC.",
      loading: "Đang tải..."
    },
    es: {
      wifi: "🟢 Comunicación directa con PC (Protección 1:1)",
      tabUpload: "📤 Enviar a la PC",
      tabDownload: "📥 Recibir de la PC",
      titleUpload: "📱 Transferir archivos a la PC",
      descUpload: "Seleccione archivos o fotos para transferirlos directamente a su PC.",
      titleDownload: "📥 Descargar archivos de la PC",
      descDownload: "Toque los archivos a continuación para descargarlos en su teléfono.",
      security: "🔒 * Transferencia segura y rápida en su Wi-Fi local sin pasar por Internet.",
      selectFiles: "📄 Seleccionar archivos (Fotos, PDF, Zip, etc.)",
      capture: "📷 Tomar foto y enviar",
      dropHint: "o arrastre y suelte archivos aquí",
      selectedCount: "Archivos seleccionados: {0}",
      startUpload: "🚀 Iniciar transferencia ({0})",
      transferring: "🔄 Transfiriendo a la PC... ({0}%)",
      success: "✅ ¡Transferencia completada!<br/>Archivos guardados en su PC.",
      sendMore: "➕ Enviar más archivos",
      fail: "⚠️ Falló la transferencia (Estado: {0}). Inténtelo de nuevo.",
      netErr: "⚠️ Error de red: ",
      pcFilesHeader: "Archivos compartidos de la PC:",
      refreshFiles: "🔄 Actualizar",
      btnDownload: "⬇️ Descargar",
      emptyDownload: "📂 No hay archivos compartidos en la PC.<br/>Añada archivos desde la aplicación en la PC.",
      loading: "Cargando..."
    },
    de: {
      wifi: "🟢 Direkte PC-Verbindung (1:1 Schutz)",
      tabUpload: "📤 An PC senden",
      tabDownload: "📥 Vom PC empfangen",
      titleUpload: "📱 Dateien auf den PC übertragen",
      descUpload: "Wählen Sie Dateien oder Fotos aus, um sie direkt auf Ihren PC zu übertragen.",
      titleDownload: "📥 Dateien vom PC herunterladen",
      descDownload: "Tippen Sie auf Dateien, um sie auf Ihr Smartphone herunterzuladen.",
      security: "🔒 * Sicher und schnell im lokalen WLAN ohne Internetübertragung.",
      selectFiles: "📄 Dateien auswählen (Fotos, PDF, Zip etc.)",
      capture: "📷 Foto aufnehmen & senden",
      dropHint: "oder Dateien hierher ziehen",
      selectedCount: "Ausgewählte Dateien: {0}",
      startUpload: "🚀 Übertragung starten ({0})",
      transferring: "🔄 Wird auf PC übertragen... ({0}%)",
      success: "✅ Übertragung erfolgreich abgeschlossen!<br/>Dateien wurden sicher gespeichert.",
      sendMore: "➕ Weitere Dateien senden",
      fail: "⚠️ Übertragung fehlgeschlagen (Status: {0}). Bitte erneut versuchen.",
      netErr: "⚠️ Netzwerkfehler: ",
      pcFilesHeader: "Vom PC freigegebene Dateien:",
      refreshFiles: "🔄 Aktualisieren",
      btnDownload: "⬇️ Herunterladen",
      emptyDownload: "📂 Keine Dateien im PC-Freigabeordner.<br/>Bitte fügen Sie Dateien am PC hinzu.",
      loading: "Wird geladen..."
    },
    fr: {
      wifi: "🟢 Communication directe PC (Protection 1:1)",
      tabUpload: "📤 Envoyer au PC",
      tabDownload: "📥 Recevoir du PC",
      titleUpload: "📱 Transférer des fichiers vers le PC",
      descUpload: "Sélectionnez des fichiers ou photos pour les transférer sur votre PC.",
      titleDownload: "📥 Télécharger des fichiers depuis le PC",
      descDownload: "Touchez les fichiers ci-dessous pour les télécharger sur votre téléphone.",
      security: "🔒 * Transfert direct et sécurisé sur votre Wi-Fi local sans passer par Internet.",
      selectFiles: "📄 Sélectionner des fichiers (Photos, PDF, Zip...)",
      capture: "📷 Prendre une photo & envoyer",
      dropHint: "ou glissez-déposez des fichiers ici",
      selectedCount: "Fichiers sélectionnés : {0}",
      startUpload: "🚀 Lancer le transfert ({0})",
      transferring: "🔄 Transfert vers le PC en cours... ({0}%)",
      success: "✅ Transfert réussi !<br/>Fichiers enregistrés en toute sécurité.",
      sendMore: "➕ Envoyer d'autres fichiers",
      fail: "⚠️ Échec du transfert (Statut : {0}). Veuillez réessayer.",
      netErr: "⚠️ Erreur réseau : ",
      pcFilesHeader: "Fichiers partagés du PC :",
      refreshFiles: "🔄 Actualiser",
      btnDownload: "⬇️ Télécharger",
      emptyDownload: "📂 Aucun fichier partagé sur le PC.<br/>Veuillez ajouter des fichiers depuis l'application PC.",
      loading: "Chargement..."
    },
    pt: {
      wifi: "🟢 Conexão direta com PC (Proteção 1:1)",
      tabUpload: "📤 Enviar para PC",
      tabDownload: "📥 Receber do PC",
      titleUpload: "📱 Transferir arquivos para o PC",
      descUpload: "Selecione arquivos ou fotos para transferir diretamente para o PC.",
      titleDownload: "📥 Baixar arquivos do PC",
      descDownload: "Toque nos arquivos abaixo para baixá-los no seu celular.",
      security: "🔒 * Transferência rápida e segura na rede Wi-Fi local sem passar pela Internet.",
      selectFiles: "📄 Selecionar arquivos (Fotos, PDF, Zip etc.)",
      capture: "📷 Tirar foto e enviar",
      dropHint: "ou arraste e solte arquivos aqui",
      selectedCount: "Arquivos selecionados: {0}",
      startUpload: "🚀 Iniciar transferência ({0})",
      transferring: "🔄 Transferindo para o PC... ({0}%)",
      success: "✅ Transferência concluída!<br/>Arquivos salvos com sucesso.",
      sendMore: "➕ Enviar mais arquivos",
      fail: "⚠️ Falha na transferência (Status: {0}). Tente novamente.",
      netErr: "⚠️ Erro de rede: ",
      pcFilesHeader: "Arquivos compartilhados do PC:",
      refreshFiles: "🔄 Atualizar",
      btnDownload: "⬇️ Baixar",
      emptyDownload: "📂 Nenhum arquivo compartilhado no PC.<br/>Adicione arquivos pelo aplicativo no PC.",
      loading: "Carregando..."
    },
    it: {
      wifi: "🟢 Connessione diretta PC (Protezione 1:1)",
      tabUpload: "📤 Invia al PC",
      tabDownload: "📥 Ricevi dal PC",
      titleUpload: "📱 Trasferisci file sul PC",
      descUpload: "Seleziona file o foto qui sotto per trasferirli direttamente sul PC.",
      titleDownload: "📥 Scarica file dal PC",
      descDownload: "Tocca i file qui sotto per scaricarli direttamente sullo smartphone.",
      security: "🔒 * Trasferimento sicuro e veloce sulla rete Wi-Fi locale senza passare da Internet.",
      selectFiles: "📄 Seleziona file (Foto, PDF, Zip...)",
      capture: "📷 Scatta foto e invia",
      dropHint: "o trascina i file qui",
      selectedCount: "File selezionati: {0}",
      startUpload: "🚀 Avvia trasferimento ({0})",
      transferring: "🔄 Trasferimento al PC in corso... ({0}%)",
      success: "✅ Trasferimento completato!<br/>File salvati con successo sul PC.",
      sendMore: "➕ Invia altri file",
      fail: "⚠️ Trasferimento non riuscito (Stato: {0}). Riprova.",
      netErr: "⚠️ Errore di red: ",
      pcFilesHeader: "File condivisi dal PC:",
      refreshFiles: "🔄 Aggiorna",
      btnDownload: "⬇️ Scarica",
      emptyDownload: "📂 Nessun file condiviso sul PC.<br/>Aggiungi file dall'applicazione sul PC.",
      loading: "Caricamento in corso..."
    }
  };

  function getLangDict() {
    return I18N[currentLang] || I18N.ja;
  }

  function formatStr(template, ...args) {
    return template.replace(/{(\d+)}/g, (match, number) => typeof args[number] !== 'undefined' ? args[number] : match);
  }

  function applyLanguage(lang) {
    currentLang = lang;
    const d = getLangDict();
    document.getElementById('lblWifiBadge').innerText = d.wifi;
    document.getElementById('lblTabUpload').innerText = d.tabUpload;
    document.getElementById('lblTabDownload').innerText = d.tabDownload;
    document.getElementById('lblSecurity').innerText = d.security;
    document.getElementById('lblSelectFiles').innerText = d.selectFiles;
    document.getElementById('lblCapture').innerText = d.capture;
    document.getElementById('lblDropHint').innerText = d.dropHint;
    document.getElementById('lblPcFilesHeader').innerText = d.pcFilesHeader;
    document.getElementById('lblRefreshFiles').innerText = d.refreshFiles;
    document.getElementById('langSelector').value = currentLang;

    updateHeaderForMode();

    if (pendingFiles.length > 0) {
      document.getElementById('lblSelectedCount').innerText = formatStr(d.selectedCount, pendingFiles.length);
      document.getElementById('lblStartUpload').innerText = formatStr(d.startUpload, pendingFiles.length);
    }
  }

  function updateHeaderForMode() {
    const d = getLangDict();
    if (currentMode === 'download') {
      document.getElementById('lblTitle').innerText = d.titleDownload;
      document.getElementById('lblDesc').innerText = d.descDownload;
    } else {
      document.getElementById('lblTitle').innerText = d.titleUpload;
      document.getElementById('lblDesc').innerText = d.descUpload;
    }
  }

  function switchMode(mode) {
    currentMode = mode;
    const tabUploadBtn = document.getElementById('tabUploadBtn');
    const tabDownloadBtn = document.getElementById('tabDownloadBtn');
    const uploadArea = document.getElementById('uploadArea');
    const downloadArea = document.getElementById('downloadArea');

    if (mode === 'download') {
      tabUploadBtn.classList.remove('active');
      tabDownloadBtn.classList.add('active');
      uploadArea.style.display = 'none';
      downloadArea.style.display = 'block';
      fetchDownloadFiles();
    } else {
      tabDownloadBtn.classList.remove('active');
      tabUploadBtn.classList.add('active');
      downloadArea.style.display = 'none';
      uploadArea.style.display = 'block';
    }
    updateHeaderForMode();
  }

  function changeLanguage(lang) {
    applyLanguage(lang);
  }

  // 初期化
  window.addEventListener('DOMContentLoaded', () => {
    const selector = document.getElementById('langSelector');
    let target = currentLang;
    if (!I18N[target]) {
      target = "ja";
    }
    selector.value = target;
    applyLanguage(target);
    switchMode(currentMode);
  });

  // ==============================================
  // ダウンロード一覧の取得 & 描画
  // ==============================================
  async function fetchDownloadFiles() {
    const container = document.getElementById('downloadFilesContainer');
    const d = getLangDict();
    container.innerHTML = `<div class="empty-download-state">${d.loading}</div>`;

    try {
      const resp = await fetch('/api/download-files?token=' + encodeURIComponent(SESSION_TOKEN), {
        headers: { 'X-Session-Token': SESSION_TOKEN }
      });
      if (!resp.ok) {
        throw new Error('HTTP ' + resp.status);
      }
      const files = await resp.json();
      renderDownloadFiles(files);
    } catch (err) {
      container.innerHTML = `
        <div class="empty-download-state" style="color:var(--error);">
          ⚠️ ${d.netErr} ${escapeHtml(err.message || '')}
        </div>`;
    }
  }

  function renderDownloadFiles(files) {
    const container = document.getElementById('downloadFilesContainer');
    const d = getLangDict();
    container.innerHTML = '';

    if (!files || files.length === 0) {
      container.innerHTML = `<div class="empty-download-state">${d.emptyDownload}</div>`;
      return;
    }

    files.forEach(file => {
      const item = document.createElement('div');
      item.className = 'download-item';
      const encodedName = encodeURIComponent(file.fileName);
      const downloadUrl = `/download?file=${encodedName}&token=${encodeURIComponent(SESSION_TOKEN)}`;

      item.innerHTML = `
        <div class="item-left">
          <span class="item-icon">${escapeHtml(file.typeIcon || '📄')}</span>
          <div class="item-details">
            <div class="item-title" title="${escapeHtml(file.fileName)}">${escapeHtml(file.fileName)}</div>
            <div class="item-meta">${escapeHtml(file.formattedSize || '')}  •  ${escapeHtml(file.formattedTime || '')}</div>
          </div>
        </div>
        <a class="btn-item-download" href="${downloadUrl}" download="${escapeHtml(file.fileName)}">
          ${d.btnDownload}
        </a>
      `;
      container.appendChild(item);
    });
  }

  // ==============================================
  // アップロード処理
  // ==============================================
  const fileInput = document.getElementById('fileInput');
  const cameraInput = document.getElementById('cameraInput');
  const dropZone = document.getElementById('dropZone');
  const actionArea = document.getElementById('actionArea');
  const selectedListContainer = document.getElementById('selectedListContainer');
  const fileItemsList = document.getElementById('fileItemsList');
  const progressContainer = document.getElementById('progressContainer');
  const progressBar = document.getElementById('progressBar');
  const progressStatus = document.getElementById('progressStatus');
  const resultBox = document.getElementById('resultBox');

  function formatBytes(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  }

  function handleFiles(files) {
    if (!files || files.length === 0) return;
    pendingFiles = Array.from(files);
    renderSelectedFiles();
  }

  function renderSelectedFiles() {
    fileItemsList.innerHTML = '';
    const d = getLangDict();
    document.getElementById('lblSelectedCount').innerText = formatStr(d.selectedCount, pendingFiles.length);
    document.getElementById('lblStartUpload').innerText = formatStr(d.startUpload, pendingFiles.length);

    pendingFiles.forEach((file, index) => {
      const item = document.createElement('div');
      item.className = 'file-item';
      item.innerHTML = `
        <div class="file-info">
          <div class="file-name">${escapeHtml(file.name)}</div>
          <div class="file-size">${formatBytes(file.size)}</div>
        </div>
        <button style="border:none;background:transparent;cursor:pointer;font-size:16px;color:#EF4444;" onclick="removeFile(${index})">✕</button>
      `;
      fileItemsList.appendChild(item);
    });

    selectedListContainer.style.display = pendingFiles.length > 0 ? 'block' : 'none';
    resultBox.style.display = 'none';
  }

  window.removeFile = function(index) {
    pendingFiles.splice(index, 1);
    renderSelectedFiles();
  };

  function escapeHtml(text) {
    const div = document.createElement('div');
    div.innerText = text;
    return div.innerHTML;
  }

  fileInput.addEventListener('change', (e) => {
    handleFiles(e.target.files);
    fileInput.value = '';
  });

  cameraInput.addEventListener('change', (e) => {
    handleFiles(e.target.files);
    cameraInput.value = '';
  });

  // ドラッグ＆ドロップ
  dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('dragover');
  });
  dropZone.addEventListener('dragleave', () => {
    dropZone.classList.remove('dragover');
  });
  dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('dragover');
    if (e.dataTransfer && e.dataTransfer.files) {
      handleFiles(e.dataTransfer.files);
    }
  });

  async function startUpload() {
    if (pendingFiles.length === 0) return;

    actionArea.style.display = 'none';
    selectedListContainer.style.display = 'none';
    progressContainer.style.display = 'block';
    resultBox.style.display = 'none';

    const d = getLangDict();
    const totalFiles = pendingFiles.length;
    let completedFiles = 0;

    try {
      for (let i = 0; i < totalFiles; i++) {
        const file = pendingFiles[i];
        const progressBase = (i / totalFiles) * 100;

        await uploadSingleFile(file, (percent) => {
          const currentTotalPercent = Math.round(progressBase + (percent / totalFiles));
          progressBar.style.width = currentTotalPercent + '%';
          progressStatus.innerText = formatStr(d.transferring, currentTotalPercent) + ` (${i + 1}/${totalFiles})`;
        });

        completedFiles++;
      }

      progressBar.style.width = '100%';
      progressStatus.innerText = formatStr(d.transferring, 100);

      setTimeout(() => {
        progressContainer.style.display = 'none';
        resultBox.className = 'result-ok';
        resultBox.innerHTML = `
          <div>${d.success}</div>
          <div style="font-size:12px;margin-top:8px;font-weight:normal;opacity:0.9;">${completedFiles} 件のファイルを保存しました</div>
          <button class="btn-reset" onclick="resetForm()">${d.sendMore}</button>
        `;
        resultBox.style.display = 'block';
      }, 400);

    } catch (err) {
      progressContainer.style.display = 'none';
      resultBox.className = 'result-err';
      resultBox.innerHTML = `
        <div>${d.netErr} ${escapeHtml(err.message || '')}</div>
        <button class="btn-reset" onclick="resetForm()">再試行</button>
      `;
      resultBox.style.display = 'block';
    }
  }

  function uploadSingleFile(file, onProgress) {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      const encodedName = encodeURIComponent(file.name);
      xhr.open('POST', '/upload?name=' + encodedName + '&token=' + encodeURIComponent(SESSION_TOKEN), true);
      xhr.setRequestHeader('X-Filename', encodedName);
      xhr.setRequestHeader('X-Session-Token', SESSION_TOKEN);
      xhr.setRequestHeader('Content-Type', 'application/octet-stream');

      xhr.upload.onprogress = (e) => {
        if (e.lengthComputable) {
          const percent = Math.round((e.loaded / e.total) * 100);
          onProgress(percent);
        }
      };

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          onProgress(100);
          resolve();
        } else {
          const d = getLangDict();
          reject(new Error(formatStr(d.fail, xhr.status)));
        }
      };

      xhr.onerror = () => {
        const d = getLangDict();
        reject(new Error(d.netErr));
      };

      xhr.send(file);
    });
  }

  window.resetForm = function() {
    pendingFiles = [];
    actionArea.style.display = 'block';
    selectedListContainer.style.display = 'none';
    progressContainer.style.display = 'none';
    resultBox.style.display = 'none';
    progressBar.style.width = '0%';
  };
</script>
</body>
</html>
""";

    #endregion
}
