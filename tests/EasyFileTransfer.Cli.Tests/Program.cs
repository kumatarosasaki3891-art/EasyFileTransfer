using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EasyFileTransfer.Cli;
using EasyFileTransfer.Cli.Services;
using EasyFileTransfer.Services;

int passed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (!condition)
    {
        throw new InvalidOperationException(detail == null ? $"FAILED: {name}" : $"FAILED: {name} ({detail})");
    }
    passed++;
    Console.WriteLine($"PASS: {name}");
}

int GetFreeTcpPort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

string FindEftBinary(out bool useDotnetHost)
{
    var exe = Path.Combine(AppContext.BaseDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "eft.exe" : "eft");
    if (File.Exists(exe))
    {
        useDotnetHost = false;
        return exe;
    }
    var dll = Path.Combine(AppContext.BaseDirectory, "eft.dll");
    if (File.Exists(dll))
    {
        useDotnetHost = true;
        return dll;
    }
    throw new FileNotFoundException("Could not find eft binary or eft.dll in test output directory.");
}

async Task<(int ExitCode, string StdOut, string StdErr)> RunEftAsync(
    string[] args,
    IDictionary<string, string>? envOverrides = null)
{
    var bin = FindEftBinary(out bool useDotnetHost);
    var psi = new ProcessStartInfo
    {
        FileName = useDotnetHost ? "dotnet" : bin,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };
    if (useDotnetHost)
    {
        psi.ArgumentList.Add(bin);
    }
    foreach (var a in args)
    {
        psi.ArgumentList.Add(a);
    }
    if (envOverrides != null)
    {
        foreach (var kv in envOverrides)
        {
            psi.Environment[kv.Key] = kv.Value;
        }
    }

    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start eft process.");
    var outTask = proc.StandardOutput.ReadToEndAsync();
    var errTask = proc.StandardError.ReadToEndAsync();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await proc.WaitForExitAsync(cts.Token);
    return (proc.ExitCode, await outTask, await errTask);
}

var tempRoot = Path.Combine(Path.GetTempPath(), $"eft_cli_tests_{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    // =========================================================================
    // 1. オプション解析テスト
    // =========================================================================
    var defParse = CliOptionsParser.Parse([], AppLanguage.Japanese);
    Check("Default options parse succeeds", defParse.Options != null);
    Check("Default port is 5050", defParse.Options!.Port == 5050);
    Check("Default timeout is 30", defParse.Options.TimeoutMinutes == 30);
    Check("Default once/no-qr/quiet are false", !defParse.Options.Once && !defParse.Options.NoQr && !defParse.Options.Quiet);

    var allLong = CliOptionsParser.Parse(
        ["--port", "6060", "--dir", "/tmp/in", "--share", "/tmp/out", "--ip", "192.168.1.25", "--lang", "en", "--timeout", "45", "--once", "--no-qr", "--qr-plain", "--no-color", "--quiet"],
        AppLanguage.Japanese);
    Check("Long flags parse succeeds", allLong.Options != null);
    Check("Long flags values applied",
        allLong.Options!.Port == 6060
        && allLong.Options.ReceiveDir == "/tmp/in"
        && allLong.Options.ShareDir == "/tmp/out"
        && allLong.Options.IpAddress == "192.168.1.25"
        && allLong.Options.Language == AppLanguage.English
        && allLong.Options.TimeoutMinutes == 45
        && allLong.Options.Once
        && allLong.Options.NoQr
        && allLong.Options.QrPlain
        && allLong.Options.NoColor
        && allLong.Options.Quiet);

    var shortAndEq = CliOptionsParser.Parse(
        ["-p", "7070", "-d=/tmp/a", "-s", "/tmp/b", "--ip=10.0.0.5", "-l=zh-TW", "-t", "15", "-q"],
        AppLanguage.Japanese);
    Check("Short and --opt=val flags parse succeeds", shortAndEq.Options != null);
    Check("Short and --opt=val values applied",
        shortAndEq.Options!.Port == 7070
        && shortAndEq.Options.ReceiveDir == "/tmp/a"
        && shortAndEq.Options.ShareDir == "/tmp/b"
        && shortAndEq.Options.IpAddress == "10.0.0.5"
        && shortAndEq.Options.Language == AppLanguage.TraditionalChinese
        && shortAndEq.Options.TimeoutMinutes == 15
        && shortAndEq.Options.Quiet);

    Check("Version flag sets ShowVersion", CliOptionsParser.Parse(["--version"], AppLanguage.Japanese).Options!.ShowVersion);
    Check("Short -v sets ShowVersion", CliOptionsParser.Parse(["-v"], AppLanguage.Japanese).Options!.ShowVersion);
    Check("Help flag sets ShowHelp", CliOptionsParser.Parse(["--help"], AppLanguage.Japanese).Options!.ShowHelp);
    Check("Short -h sets ShowHelp", CliOptionsParser.Parse(["-h"], AppLanguage.Japanese).Options!.ShowHelp);

    foreach (var badPort in new[] { "0", "65536", "abc", "-1" })
    {
        var r = CliOptionsParser.Parse(["--port", badPort], AppLanguage.English);
        Check($"Invalid port '{badPort}' rejected", r.Options == null && !string.IsNullOrWhiteSpace(r.ErrorMessage));
    }

    foreach (var badTimeout in new[] { "0", "-5", "1441", "abc" })
    {
        var r = CliOptionsParser.Parse(["--timeout", badTimeout], AppLanguage.English);
        Check($"Invalid timeout '{badTimeout}' rejected", r.Options == null && !string.IsNullOrWhiteSpace(r.ErrorMessage));
    }

    foreach (var badIp in new[] { "not-an-ip", "::1", "999.1.1.1" })
    {
        var r = CliOptionsParser.Parse(["--ip", badIp], AppLanguage.English);
        Check($"Invalid IP '{badIp}' rejected", r.Options == null && !string.IsNullOrWhiteSpace(r.ErrorMessage));
    }

    Check("Invalid lang 'xx' rejected", CliOptionsParser.Parse(["--lang", "xx"], AppLanguage.English).Options == null);
    Check("Unknown option '--unknown' rejected", CliOptionsParser.Parse(["--unknown"], AppLanguage.English).Options == null);
    Check("Missing option value rejected", CliOptionsParser.Parse(["--port"], AppLanguage.English).Options == null);
    Check("Empty --dir= rejected", CliOptionsParser.Parse(["--dir="], AppLanguage.English).Options == null);

    var sendSub = CliOptionsParser.Parse(["send"], AppLanguage.English);
    Check("Positional 'send' rejected with no-subcommands hint",
        sendSub.Options == null && sendSub.ErrorMessage!.Contains("subcommands", StringComparison.OrdinalIgnoreCase));
    var recvSub = CliOptionsParser.Parse(["receive"], AppLanguage.English);
    Check("Positional 'receive' rejected with no-subcommands hint",
        recvSub.Options == null && recvSub.ErrorMessage!.Contains("subcommands", StringComparison.OrdinalIgnoreCase));

    // =========================================================================
    // 2. 11言語パース＆ひらがな・カタカナ混入チェック
    // =========================================================================
    var expectedCodes = new (string Input, AppLanguage ExpectedLang, string ExpectedCanonical)[]
    {
        ("ja", AppLanguage.Japanese, "ja"),
        ("ja-JP", AppLanguage.Japanese, "ja"),
        ("en", AppLanguage.English, "en"),
        ("en-US", AppLanguage.English, "en"),
        ("zh", AppLanguage.Chinese, "zh"),
        ("zh-CN", AppLanguage.Chinese, "zh"),
        ("zh-TW", AppLanguage.TraditionalChinese, "zh-TW"),
        ("zh_tw", AppLanguage.TraditionalChinese, "zh-TW"),
        ("zh-Hant", AppLanguage.TraditionalChinese, "zh-TW"),
        ("ko", AppLanguage.Korean, "ko"),
        ("ko-KR", AppLanguage.Korean, "ko"),
        ("vi", AppLanguage.Vietnamese, "vi"),
        ("vi-VN", AppLanguage.Vietnamese, "vi"),
        ("es", AppLanguage.Spanish, "es"),
        ("es-ES", AppLanguage.Spanish, "es"),
        ("de", AppLanguage.German, "de"),
        ("de-DE", AppLanguage.German, "de"),
        ("fr", AppLanguage.French, "fr"),
        ("fr-FR", AppLanguage.French, "fr"),
        ("pt", AppLanguage.Portuguese, "pt"),
        ("pt-BR", AppLanguage.Portuguese, "pt"),
        ("it", AppLanguage.Italian, "it"),
        ("it-IT", AppLanguage.Italian, "it")
    };

    foreach (var (input, expLang, expCode) in expectedCodes)
    {
        bool ok = CliMessages.TryParseLanguageCode(input, out var parsed, out var code);
        Check($"Language alias '{input}' -> {expCode}", ok && parsed == expLang && code == expCode);
    }

    var kanaRegex = new Regex(@"[\u3040-\u309F\u30A1-\u30FA\u30FC-\u30FF]");
    foreach (var lang in CliMessages.AllLanguages)
    {
        foreach (var key in CliMessages.AllKeys)
        {
            bool exists = CliMessages.TryGetRawEntry(lang, key, out var text);
            if (!exists || string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException($"Missing or empty translation for ({lang}, {key})");
            }
            if (lang != AppLanguage.Japanese && kanaRegex.IsMatch(text))
            {
                throw new InvalidOperationException($"Japanese kana leaked in ({lang}, {key}): {text}");
            }
        }
    }
    Check($"All {CliMessages.AllKeys.Count} CLI message keys exist in all 11 languages with zero kana leaks", true);

    // =========================================================================
    // 3. 端末 QR コード描画チェック
    // =========================================================================
    string sampleUrl = "http://192.168.100.123:5050/?mode=upload&token=a1b2c3d4&lang=zh-TW";
    string qrAnsi = TerminalQrRenderer.RenderToTerminal(sampleUrl, useAnsiColor: true);
    string qrPlain = TerminalQrRenderer.RenderToTerminal(sampleUrl, useAnsiColor: false);

    Check("QR ANSI contains foreground/background escape code", qrAnsi.Contains("\u001b[30;47m") && qrAnsi.Contains("\u001b[0m"));
    Check("QR ANSI contains half-block glyphs", qrAnsi.Contains('█') || qrAnsi.Contains('▀') || qrAnsi.Contains('▄'));
    Check("QR plain contains no ESC codes", !qrPlain.Contains('\u001b'));

    var ansiLines = qrAnsi.Split(Environment.NewLine);
    var plainLines = qrPlain.Split(Environment.NewLine);
    Check("QR ANSI and plain have identical line count", ansiLines.Length == plainLines.Length && plainLines.Length >= 17);

    int expectedCols = plainLines[0].Length;
    Check("QR quiet zone width >= 29 and uniform across all lines",
        expectedCols >= 29 && plainLines.All(line => line.Length == expectedCols));
    // 4-module top quiet zone = first 2 terminal lines are all spaces
    Check("QR top 4-module quiet zone has 2 blank rows",
        plainLines[0].All(c => c == ' ') && plainLines[1].All(c => c == ' '));
    // First and last 4 columns are quiet zone spaces on every line
    Check("QR left/right 4-module quiet zone is blank on all lines",
        plainLines.All(line => line[..4].All(c => c == ' ') && line[^4..].All(c => c == ' ')));

    // =========================================================================
    // 4. セッション生成・検証（8文字16進数 + 1:1 IP固定 + 有効期限）
    // =========================================================================
    using (var srv = new FileTransferServer())
    {
        var token = srv.CreateNewSession(TransferMode.Upload);
        Check("Session token is 8 lowercase hex chars", Regex.IsMatch(token, "^[0-9a-f]{8}$"), token);
        Check("ValidateSession rejects null/empty/wrong token",
            srv.ValidateSession(null, "10.0.0.2") == SessionValidationResult.InvalidToken
            && srv.ValidateSession("", "10.0.0.2") == SessionValidationResult.InvalidToken
            && srv.ValidateSession("deadbeef", "10.0.0.2") == SessionValidationResult.InvalidToken);

        Check("ValidateSession accepts valid token and binds first client IP",
            srv.ValidateSession(token, "192.168.1.20") == SessionValidationResult.Valid
            && srv.CurrentSession!.BoundClientIp == "192.168.1.20");

        Check("ValidateSession rejects different IP with IpMismatch",
            srv.ValidateSession(token, "192.168.1.99") == SessionValidationResult.IpMismatch);

        Check("ValidateSession still allows bound IP",
            srv.ValidateSession(token, "192.168.1.20") == SessionValidationResult.Valid);

        srv.CurrentSession!.ExpiresAt = DateTime.UtcNow.AddSeconds(-5);
        Check("ValidateSession returns Expired after expiry",
            srv.ValidateSession(token, "192.168.1.20") == SessionValidationResult.Expired);
    }

    // =========================================================================
    // 5. セッション期限切れ時の終了コード 1 検証 (in-process)
    // =========================================================================
    {
        var expOut = new StringWriter();
        var expErr = new StringWriter();
        var expDir = Path.Combine(tempRoot, "exp_up");
        var expShare = Path.Combine(tempRoot, "exp_share");
        Directory.CreateDirectory(expDir);
        Directory.CreateDirectory(expShare);
        int expPort = GetFreeTcpPort();

        int exitCode = await CliApp.RunAsync(
            ["--port", expPort.ToString(), "--dir", expDir, "--share", expShare, "--ip", "127.0.0.1", "--lang", "en", "--no-qr"],
            expOut,
            expErr,
            isOutputRedirectedOverride: true,
            onServerStarted: s =>
            {
                if (s.CurrentSession != null)
                {
                    s.CurrentSession.ExpiresAt = DateTime.UtcNow.AddMilliseconds(250);
                }
            });

        Check("Expired session with 0 transfers returns exit code 1", exitCode == 1, $"got {exitCode}");
        Check("Expired session prints expiry message to stderr", expErr.ToString().Contains("expired", StringComparison.OrdinalIgnoreCase));
    }

    // =========================================================================
    // 6. 実バイナリ (eft) によるプロセス外 CLI & ライブ HTTP E2E テスト
    // =========================================================================
    var isolatedConfigRoot = Path.Combine(tempRoot, "isolated_cfg");
    var cfgAppDir = Path.Combine(isolatedConfigRoot, "EasyFileTransfer");
    Directory.CreateDirectory(cfgAppDir);
    var seedConfigPath = Path.Combine(cfgAppDir, "config.json");
    var seedConfigJson = """
{
  "TargetFolder": "",
  "DownloadFolder": "",
  "Language": "de"
}
""";
    File.WriteAllText(seedConfigPath, seedConfigJson, Encoding.UTF8);
    var childEnv = new Dictionary<string, string>
    {
        ["APPDATA"] = isolatedConfigRoot,
        ["XDG_CONFIG_HOME"] = isolatedConfigRoot
    };

    var verRes = await RunEftAsync(["--version"], childEnv);
    Check("eft --version returns 0 and prints v1.1.0", verRes.ExitCode == 0 && verRes.StdOut.Contains("v1.1.0") && verRes.StdOut.Contains("Releases:"));

    var helpDeFromCfgRes = await RunEftAsync(["--help"], childEnv);
    Check("eft --help respects config.json language ('de')",
        helpDeFromCfgRes.ExitCode == 0 && helpDeFromCfgRes.StdOut.Contains("Verwendung: eft"),
        $"exit={helpDeFromCfgRes.ExitCode}, out={helpDeFromCfgRes.StdOut}, err={helpDeFromCfgRes.StdErr}");

    var helpEnRes = await RunEftAsync(["--lang", "en", "--help"], childEnv);
    Check("eft --lang en --help returns 0 without kana",
        helpEnRes.ExitCode == 0 && helpEnRes.StdOut.Contains("Usage: eft") && !kanaRegex.IsMatch(helpEnRes.StdOut));

    var badOptRes = await RunEftAsync(["--not-a-real-flag"], childEnv);
    Check("eft --not-a-real-flag returns exit code 2", badOptRes.ExitCode == 2);

    var subCmdRes = await RunEftAsync(["send"], childEnv);
    Check("eft send returns exit code 2", subCmdRes.ExitCode == 2);

    var missingShareRes = await RunEftAsync(["--share", Path.Combine(tempRoot, "does_not_exist")], childEnv);
    Check("eft --share <nonexistent> returns exit code 2", missingShareRes.ExitCode == 2);

    // 6a. ダウンロード + --once + セッション拒否 + favicon + 言語反映 E2E
    {
        var upDir = Path.Combine(tempRoot, "e2e_dl_up");
        var shareDir = Path.Combine(tempRoot, "e2e_dl_share");
        Directory.CreateDirectory(upDir);
        Directory.CreateDirectory(shareDir);

        var sharedFileName = "hello-eft.txt";
        var sharedPayload = Encoding.UTF8.GetBytes("EasyFileTransfer CLI live download check 12345!");
        File.WriteAllBytes(Path.Combine(shareDir, sharedFileName), sharedPayload);

        int port = GetFreeTcpPort();
        var bin = FindEftBinary(out bool useDotnetHost);
        var psi = new ProcessStartInfo
        {
            FileName = useDotnetHost ? "dotnet" : bin,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        if (useDotnetHost) psi.ArgumentList.Add(bin);
        foreach (var a in new[] { "--port", port.ToString(), "--dir", upDir, "--share", shareDir, "--ip", "127.0.0.1", "--lang", "en", "--no-qr", "--once" })
        {
            psi.ArgumentList.Add(a);
        }
        foreach (var kv in childEnv) psi.Environment[kv.Key] = kv.Value;

        using var proc = Process.Start(psi)!;
        try
        {
            var stdOutLines = new List<string>();
            string? detectedUrl = null;
            var urlFoundTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            var readOutTask = Task.Run(async () =>
            {
                while (await proc.StandardOutput.ReadLineAsync() is { } line)
                {
                    stdOutLines.Add(line);
                    var m = Regex.Match(line, @"http://127\.0\.0\.1:\d+/\?[^\s]+");
                    if (m.Success && detectedUrl == null)
                    {
                        detectedUrl = m.Value;
                        urlFoundTcs.TrySetResult(detectedUrl);
                    }
                }
            });
            var errTask = proc.StandardError.ReadToEndAsync();

            var url = await urlFoundTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var tokenMatch = Regex.Match(url, @"token=([0-9a-f]{8})");
            Check("E2E Download parsed valid URL with 8-hex token and lang=en",
                tokenMatch.Success && url.Contains("lang=en"), url);
            var token = tokenMatch.Groups[1].Value;
            var baseUrl = $"http://127.0.0.1:{port}";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // 1) favicon.ico は 404 で終了し、InvalidToken を出力しない
            var favResp = await http.GetAsync($"{baseUrl}/favicon.ico");
            Check("GET /favicon.ico returns 404", favResp.StatusCode == HttpStatusCode.NotFound);

            // 2) 無効トークンは 403
            var badTokenResp = await http.GetAsync($"{baseUrl}/?mode=upload&token=00000000&lang=en");
            Check("GET / with wrong token returns 403", badTokenResp.StatusCode == HttpStatusCode.Forbidden);

            // 3) 正しいトークンでモバイルページ取得
            var pageResp = await http.GetAsync(url);
            var pageHtml = await pageResp.Content.ReadAsStringAsync();
            Check("GET / with valid token returns 200 HTML containing session token",
                pageResp.StatusCode == HttpStatusCode.OK && pageHtml.Contains(token) && pageHtml.Contains("EasyFileTransfer"));

            // 4) 共有ファイル一覧 API
            var listResp = await http.GetAsync($"{baseUrl}/api/download-files?token={token}");
            var listJson = await listResp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(listJson);
            Check("GET /api/download-files returns shared file in JSON",
                listResp.StatusCode == HttpStatusCode.OK
                && doc.RootElement.ValueKind == JsonValueKind.Array
                && doc.RootElement.GetArrayLength() == 1
                && doc.RootElement[0].GetProperty("fileName").GetString() == sharedFileName);

            // 5) ダウンロード実行 → --once によりプロセス自動終了 (exit code 0)
            var dlResp = await http.GetAsync($"{baseUrl}/download?file={Uri.EscapeDataString(sharedFileName)}&token={token}");
            var dlBytes = await dlResp.Content.ReadAsByteArrayAsync();
            Check("GET /download returns 200 and exact file bytes",
                dlResp.StatusCode == HttpStatusCode.OK && dlBytes.SequenceEqual(sharedPayload));

            using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await proc.WaitForExitAsync(exitCts.Token);
            await readOutTask;
            var errOut = await errTask;

            Check("eft --once exited with code 0 after download", proc.ExitCode == 0, $"exit={proc.ExitCode}");
            var allOut = string.Join(Environment.NewLine, stdOutLines);
            Check("stdout logged Sent completion line", allOut.Contains($"Sent: {sharedFileName}"));
            Check("stderr logged bad-token rejection without false favicon error",
                errOut.Contains("Rejected", StringComparison.OrdinalIgnoreCase) && errOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length == 1,
                errOut);
        }
        finally
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        }
    }

    // 6b. アップロード + --once + 同名ファイル重複リネーム E2E
    {
        var upDir = Path.Combine(tempRoot, "e2e_ul_up");
        var shareDir = Path.Combine(tempRoot, "e2e_ul_share");
        Directory.CreateDirectory(upDir);
        Directory.CreateDirectory(shareDir);

        async Task RunUploadOnceAsync(string fileName, byte[] payload)
        {
            int port = GetFreeTcpPort();
            var bin = FindEftBinary(out bool useDotnetHost);
            var psi = new ProcessStartInfo
            {
                FileName = useDotnetHost ? "dotnet" : bin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            if (useDotnetHost) psi.ArgumentList.Add(bin);
            foreach (var a in new[] { "--port", port.ToString(), "--dir", upDir, "--share", shareDir, "--ip", "127.0.0.1", "--lang", "fr", "--no-qr", "--quiet", "--once" })
            {
                psi.ArgumentList.Add(a);
            }
            foreach (var kv in childEnv) psi.Environment[kv.Key] = kv.Value;

            using var proc = Process.Start(psi)!;
            try
            {
                var urlTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                var outLines = new List<string>();
                var readTask = Task.Run(async () =>
                {
                    while (await proc.StandardOutput.ReadLineAsync() is { } line)
                    {
                        outLines.Add(line);
                        var m = Regex.Match(line, @"http://127\.0\.0\.1:\d+/\?[^\s]+");
                        if (m.Success) urlTcs.TrySetResult(m.Value);
                    }
                });
                var errTask = proc.StandardError.ReadToEndAsync();

                var url = await urlTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var token = Regex.Match(url, @"token=([0-9a-f]{8})").Groups[1].Value;

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                using var content = new ByteArrayContent(payload);
                var resp = await http.PostAsync($"http://127.0.0.1:{port}/upload?name={Uri.EscapeDataString(fileName)}&token={token}", content);
                Check($"POST /upload '{fileName}' returned 200", resp.StatusCode == HttpStatusCode.OK);

                using var exitCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await proc.WaitForExitAsync(exitCts.Token);
                await readTask;
                var err = await errTask;
                Check($"eft --once upload '{fileName}' exited 0 with empty stderr", proc.ExitCode == 0 && string.IsNullOrWhiteSpace(err), $"exit={proc.ExitCode}, err={err}");
            }
            finally
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            }
        }

        var firstPayload = Encoding.UTF8.GetBytes("First upload content テスト");
        var secondPayload = Encoding.UTF8.GetBytes("Second upload content (should be renamed)");
        await RunUploadOnceAsync("sample.txt", firstPayload);
        await RunUploadOnceAsync("sample.txt", secondPayload);

        Check("First uploaded file saved as sample.txt with exact bytes",
            File.ReadAllBytes(Path.Combine(upDir, "sample.txt")).SequenceEqual(firstPayload));
        Check("Duplicate uploaded file saved as sample (1).txt with exact bytes",
            File.ReadAllBytes(Path.Combine(upDir, "sample (1).txt")).SequenceEqual(secondPayload));
    }

    // 6c. config.json 非破壊チェック（--dir / --share / --lang 指定で config.json が変わらないこと）
    Check("Seeded config.json was not modified by CLI runs",
        File.ReadAllText(seedConfigPath, Encoding.UTF8) == seedConfigJson);

    Console.WriteLine();
    Console.WriteLine($"ALL {passed} CHECKS PASSED!");
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { }
}
