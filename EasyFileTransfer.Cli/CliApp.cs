using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EasyFileTransfer.Cli.Services;
using EasyFileTransfer.Services;

namespace EasyFileTransfer.Cli;

/// <summary>
/// 簡単ファイル転送 CLI（eft）のアプリケーション本体です。
/// </summary>
internal static class CliApp
{
    internal const string AppVersion = "1.1.0";
    internal const string ReleasesUrl = "https://github.com/kumatarosasaki3891-art/EasyFileTransfer/releases";

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        bool? isOutputRedirectedOverride = null,
        CancellationToken externalCancellation = default,
        Action<FileTransferServer>? onServerStarted = null)
    {
        var outWriter = stdout ?? Console.Out;
        var errWriter = stderr ?? Console.Error;

        StorageSettings.Initialize();
        var initialLang = GetInitialLanguage();

        var parseResult = CliOptionsParser.Parse(args, initialLang);
        if (parseResult.Options == null)
        {
            errWriter.WriteLine(parseResult.ErrorMessage);
            errWriter.WriteLine(CliMessages.Get(parseResult.ErrorLanguage, "ErrSeeHelp"));
            errWriter.Flush();
            return 2;
        }

        var options = parseResult.Options;
        var lang = options.Language ?? initialLang;

        if (options.ShowVersion)
        {
            outWriter.WriteLine($"EasyFileTransfer CLI (eft) v{AppVersion}");
            outWriter.WriteLine($"Releases: {ReleasesUrl}");
            outWriter.Flush();
            return 0;
        }

        if (options.ShowHelp)
        {
            WriteHelp(outWriter, lang);
            outWriter.Flush();
            return 0;
        }

        // セッション内のみ有効な言語を反映（config.json は上書きしない）
        LocalizationService.Instance.CurrentLanguage = lang;

        // ディレクトリ解決と検証
        string uploadFolder;
        string downloadFolder;
        try
        {
            if (options.ReceiveDir != null)
            {
                var fullReceive = Path.GetFullPath(options.ReceiveDir);
                if (File.Exists(fullReceive))
                {
                    errWriter.WriteLine(CliMessages.Format(lang, "ErrPathIsFileFmt", fullReceive));
                    errWriter.Flush();
                    return 2;
                }
                Directory.CreateDirectory(fullReceive);
                StorageSettings.SetSessionUploadFolderOverride(fullReceive);
            }

            if (options.ShareDir != null)
            {
                var fullShare = Path.GetFullPath(options.ShareDir);
                if (File.Exists(fullShare))
                {
                    errWriter.WriteLine(CliMessages.Format(lang, "ErrPathIsFileFmt", fullShare));
                    errWriter.Flush();
                    return 2;
                }
                if (!Directory.Exists(fullShare))
                {
                    errWriter.WriteLine(CliMessages.Format(lang, "ErrShareNotFoundFmt", fullShare));
                    errWriter.Flush();
                    return 2;
                }
                StorageSettings.SetSessionDownloadFolderOverride(fullShare);
            }

            uploadFolder = StorageSettings.GetUploadFolder();
            downloadFolder = StorageSettings.GetDownloadFolder();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            var pathHint = options.ReceiveDir ?? options.ShareDir ?? string.Empty;
            errWriter.WriteLine(CliMessages.Format(lang, "ErrCreateDirFmt", pathHint, ex.Message));
            errWriter.Flush();
            StorageSettings.ResetOverrides();
            return 3;
        }

        using var server = new FileTransferServer();
        try
        {
            var token = server.CreateNewSession(TransferMode.Upload);
            if (server.CurrentSession != null)
            {
                server.CurrentSession.ExpiresAt = server.CurrentSession.CreatedAt.AddMinutes(options.TimeoutMinutes);
            }
            server.Start(options.Port);
            onServerStarted?.Invoke(server);

            var selectedIp = options.IpAddress ?? server.IpCandidates[0].IpAddress;
            var url = server.GetTransferUrl(selectedIp, TransferMode.Upload);

            bool isRedirected = isOutputRedirectedOverride ?? Console.IsOutputRedirected;
            bool showQr = !options.NoQr && !isRedirected;
            bool useQrAnsi = !options.QrPlain;
            bool useColor = !options.NoColor
                            && !isRedirected
                            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

            var consoleLock = new object();
            int activeUploads = 0;
            int completedTransfers = 0;
            var exitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            void WriteOutLine(string line)
            {
                lock (consoleLock)
                {
                    outWriter.WriteLine(line);
                    outWriter.Flush();
                }
            }

            void WriteErrLine(string line)
            {
                lock (consoleLock)
                {
                    errWriter.WriteLine(line);
                    errWriter.Flush();
                }
            }

            server.ClientIpConnected += clientIp =>
            {
                if (!options.Quiet)
                {
                    WriteOutLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventConnectedFmt", clientIp)}");
                }
            };

            server.UploadStarted += fileName =>
            {
                Interlocked.Increment(ref activeUploads);
                if (!options.Quiet)
                {
                    WriteOutLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventReceivingFmt", fileName)}");
                }
            };

            server.FileReceived += fileInfo =>
            {
                DecrementNonNegative(ref activeUploads);
                Interlocked.Increment(ref completedTransfers);
                WriteOutLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventReceivedFmt", fileInfo.FileName, fileInfo.FormattedSize, fileInfo.FullPath)}");
                if (options.Once)
                {
                    ScheduleOnceShutdown(exitTcs, lang, options.Quiet, WriteOutLine);
                }
            };

            server.FileDownloaded += fileName =>
            {
                Interlocked.Increment(ref completedTransfers);
                WriteOutLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventSentFmt", fileName)}");
                if (options.Once)
                {
                    ScheduleOnceShutdown(exitTcs, lang, options.Quiet, WriteOutLine);
                }
            };

            server.SessionValidationFailed += (clientIp, reason) =>
            {
                var reasonText = reason switch
                {
                    SessionValidationResult.Expired => CliMessages.Get(lang, "ReasonExpired"),
                    SessionValidationResult.IpMismatch => CliMessages.Get(lang, "ReasonIpMismatch"),
                    _ => CliMessages.Get(lang, "ReasonInvalidToken")
                };
                var ipDisplay = string.IsNullOrWhiteSpace(clientIp) ? "-" : clientIp;
                WriteErrLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventRejectedFmt", reasonText, ipDisplay)}");

                if (reason == SessionValidationResult.Expired && Volatile.Read(ref activeUploads) <= 0)
                {
                    int code = Volatile.Read(ref completedTransfers) > 0 ? 0 : 1;
                    exitTcs.TrySetResult(code);
                }
            };

            server.ErrorOccurred += errMsg =>
            {
                DecrementNonNegative(ref activeUploads);
                WriteErrLine($"[{DateTime.Now:HH:mm:ss}] {CliMessages.Format(lang, "EventErrorFmt", errMsg)}");
            };

            // 起動情報の表示（§6 準拠: URL・IP・ポート・保存先を必ず表示）
            lock (consoleLock)
            {
                PrintStartupBanner(
                    outWriter,
                    lang,
                    options,
                    server,
                    selectedIp,
                    url,
                    uploadFolder,
                    downloadFolder,
                    showQr,
                    useQrAnsi,
                    useColor);
                outWriter.Flush();
            }

            using var monitorCts = new CancellationTokenSource();

            ConsoleCancelEventHandler cancelHandler = (_, e) =>
            {
                e.Cancel = true;
                if (exitTcs.TrySetResult(0) && !options.Quiet)
                {
                    WriteOutLine(string.Empty);
                    WriteOutLine(CliMessages.Get(lang, "NoticeStopped"));
                }
            };
            Console.CancelKeyPress += cancelHandler;

            PosixSignalRegistration? sigTermReg = null;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    sigTermReg = PosixSignalRegistration.Create(PosixSignal.SIGTERM, ctx =>
                    {
                        ctx.Cancel = true;
                        exitTcs.TrySetResult(0);
                    });
                }
                catch { }
            }

            using var externalReg = externalCancellation.Register(() => exitTcs.TrySetResult(0));

            var timeoutMonitorTask = Task.Run(async () =>
            {
                try
                {
                    while (!exitTcs.Task.IsCompleted && !monitorCts.IsCancellationRequested)
                    {
                        await Task.Delay(250, monitorCts.Token);
                        if (server.CurrentSession?.IsExpired == true && Volatile.Read(ref activeUploads) <= 0)
                        {
                            int count = Volatile.Read(ref completedTransfers);
                            if (count == 0)
                            {
                                if (exitTcs.TrySetResult(1))
                                {
                                    WriteErrLine(CliMessages.Format(lang, "NoticeExpiredNoTransferFmt", options.TimeoutMinutes));
                                }
                            }
                            else
                            {
                                if (exitTcs.TrySetResult(0) && !options.Quiet)
                                {
                                    WriteOutLine(CliMessages.Format(lang, "NoticeExpiredCompletedFmt", options.TimeoutMinutes, count));
                                }
                            }
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { }
            });

            try
            {
                return await exitTcs.Task;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
                sigTermReg?.Dispose();
                monitorCts.Cancel();
                try { await timeoutMonitorTask; } catch { }
                server.Stop();
            }
        }
        catch (Exception ex)
        {
            errWriter.WriteLine(CliMessages.Format(lang, "ErrServerStartFmt", ex.Message));
            errWriter.Flush();
            return 3;
        }
        finally
        {
            StorageSettings.ResetOverrides();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static AppLanguage GetInitialLanguage()
        => LocalizationService.Instance.CurrentLanguage;

    private static void WriteHelp(TextWriter writer, AppLanguage lang)
    {
        writer.WriteLine($"EasyFileTransfer CLI (eft) v{AppVersion}");
        writer.WriteLine(CliMessages.Get(lang, "Description"));
        writer.WriteLine();
        writer.WriteLine(CliMessages.Get(lang, "UsageLine"));
        writer.WriteLine();
        writer.Write(CliMessages.Get(lang, "HelpOptions"));
        writer.WriteLine();
        writer.Write(CliMessages.Get(lang, "HelpExitCodes"));
        writer.WriteLine();
        writer.Write(CliMessages.Get(lang, "HelpExamples"));
    }

    private static void PrintStartupBanner(
        TextWriter writer,
        AppLanguage lang,
        CliOptions options,
        FileTransferServer server,
        string selectedIp,
        string url,
        string uploadFolder,
        string downloadFolder,
        bool showQr,
        bool useQrAnsi,
        bool useColor)
    {
        string Cyan(string s) => useColor ? $"\u001b[36m{s}\u001b[0m" : s;
        string Bold(string s) => useColor ? $"\u001b[1m{s}\u001b[0m" : s;
        string Gray(string s) => useColor ? $"\u001b[90m{s}\u001b[0m" : s;
        string Yellow(string s) => useColor ? $"\u001b[33m{s}\u001b[0m" : s;

        if (!options.Quiet)
        {
            writer.WriteLine(Bold($"EasyFileTransfer CLI (eft) v{AppVersion}"));
            writer.WriteLine();
        }

        if (showQr)
        {
            writer.WriteLine(TerminalQrRenderer.RenderToTerminal(url, useQrAnsi));
            writer.WriteLine();
        }

        int matchedIdx = -1;
        for (int i = 0; i < server.IpCandidates.Count; i++)
        {
            if (string.Equals(server.IpCandidates[i].IpAddress, selectedIp, StringComparison.OrdinalIgnoreCase))
            {
                matchedIdx = i;
                break;
            }
        }

        string ipDisplay = matchedIdx >= 0
            ? $"{server.IpCandidates[matchedIdx].DisplayText} [{matchedIdx + 1}/{server.IpCandidates.Count}]"
            : selectedIp;

        string portDisplay = server.Port != options.Port
            ? $"{server.Port} {CliMessages.Format(lang, "NotePortAutoChangedFmt", options.Port)}"
            : server.Port.ToString();

        var session = server.CurrentSession;
        string token = session?.Token ?? "--------";
        string expiresLocal = (session?.ExpiresAt.ToLocalTime() ?? DateTime.Now.AddMinutes(options.TimeoutMinutes)).ToString("HH:mm");
        string sessionDisplay = CliMessages.Format(lang, "SessionInfoFmt", token, options.TimeoutMinutes, expiresLocal);

        int sharedCount = CountSharedFiles(downloadFolder);
        string shareDisplay = $"{downloadFolder} {CliMessages.Format(lang, "SharedFileCountFmt", sharedCount)}";

        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelUrl") + ":")} {Bold(url)}");
        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelIp") + ":")} {ipDisplay}");
        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelPort") + ":")} {portDisplay}");
        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelSession") + ":")} {sessionDisplay}");
        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelReceiveDir") + ":")} {uploadFolder}");
        writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelShareDir") + ":")} {shareDisplay}");

        if (!options.Quiet)
        {
            if (matchedIdx < 0)
            {
                writer.WriteLine(Yellow(CliMessages.Get(lang, "NoteCustomIp")));
            }

            if (server.IpCandidates.Count > 1)
            {
                writer.WriteLine($"{Cyan(CliMessages.Get(lang, "LabelOtherIps") + ":")}");
                for (int i = 0; i < server.IpCandidates.Count; i++)
                {
                    if (i == matchedIdx) continue;
                    writer.WriteLine($"  - {server.IpCandidates[i].DisplayText}");
                }
                writer.WriteLine(Gray(CliMessages.Get(lang, "HintVirtualNicIp")));
            }

            if (options.Once)
            {
                writer.WriteLine(Yellow(CliMessages.Get(lang, "NoteOnceMode")));
            }

            writer.WriteLine();
            writer.WriteLine(CliMessages.Get(lang, "WaitingPrompt"));
        }
    }

    private static int CountSharedFiles(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void ScheduleOnceShutdown(
        TaskCompletionSource<int> exitTcs,
        AppLanguage lang,
        bool quiet,
        Action<string> writeOutLine)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(150);
            if (exitTcs.TrySetResult(0) && !quiet)
            {
                writeOutLine(CliMessages.Get(lang, "NoticeOnceCompleted"));
            }
        });
    }

    private static void DecrementNonNegative(ref int value)
    {
        while (true)
        {
            int current = Volatile.Read(ref value);
            if (current <= 0) return;
            if (Interlocked.CompareExchange(ref value, current - 1, current) == current) return;
        }
    }
}
