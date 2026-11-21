using EasyFileTransfer.Services;

namespace EasyFileTransfer.Cli;

/// <summary>
/// CLI 向けの11言語メッセージ辞書および言語コードパーサです。
/// </summary>
internal static class CliMessages
{
    internal static readonly IReadOnlyList<AppLanguage> AllLanguages =
    [
        AppLanguage.Japanese,
        AppLanguage.English,
        AppLanguage.Chinese,
        AppLanguage.Korean,
        AppLanguage.TraditionalChinese,
        AppLanguage.Vietnamese,
        AppLanguage.Spanish,
        AppLanguage.German,
        AppLanguage.French,
        AppLanguage.Portuguese,
        AppLanguage.Italian
    ];

    internal static IReadOnlyCollection<string> AllKeys => Japanese.Keys;

    internal static bool TryParseLanguageCode(string? input, out AppLanguage language, out string canonicalCode)
    {
        var normalized = (input ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
        switch (normalized)
        {
            case "ja":
            case "ja-jp":
            case "jpn":
                language = AppLanguage.Japanese;
                canonicalCode = "ja";
                return true;

            case "en":
            case "en-us":
            case "en-gb":
            case "eng":
                language = AppLanguage.English;
                canonicalCode = "en";
                return true;

            case "zh":
            case "zh-cn":
            case "zh-hans":
            case "zh-sg":
                language = AppLanguage.Chinese;
                canonicalCode = "zh";
                return true;

            case "zh-tw":
            case "zh-hant":
            case "zh-hk":
            case "zh-mo":
                language = AppLanguage.TraditionalChinese;
                canonicalCode = "zh-TW";
                return true;

            case "ko":
            case "ko-kr":
            case "kor":
                language = AppLanguage.Korean;
                canonicalCode = "ko";
                return true;

            case "vi":
            case "vi-vn":
            case "vie":
                language = AppLanguage.Vietnamese;
                canonicalCode = "vi";
                return true;

            case "es":
            case "es-es":
            case "es-mx":
            case "es-419":
            case "spa":
                language = AppLanguage.Spanish;
                canonicalCode = "es";
                return true;

            case "de":
            case "de-de":
            case "de-at":
            case "de-ch":
            case "deu":
            case "ger":
                language = AppLanguage.German;
                canonicalCode = "de";
                return true;

            case "fr":
            case "fr-fr":
            case "fr-ca":
            case "fra":
            case "fre":
                language = AppLanguage.French;
                canonicalCode = "fr";
                return true;

            case "pt":
            case "pt-br":
            case "pt-pt":
            case "por":
                language = AppLanguage.Portuguese;
                canonicalCode = "pt";
                return true;

            case "it":
            case "it-it":
            case "ita":
                language = AppLanguage.Italian;
                canonicalCode = "it";
                return true;

            default:
                language = AppLanguage.Japanese;
                canonicalCode = string.Empty;
                return false;
        }
    }

    internal static string Get(AppLanguage language, string key)
    {
        var dict = GetDictionary(language);
        if (dict.TryGetValue(key, out var val))
        {
            return val;
        }
        return Japanese.TryGetValue(key, out var fallback) ? fallback : key;
    }

    internal static string Format(AppLanguage language, string key, params object[] args)
    {
        var template = Get(language, key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    internal static bool TryGetRawEntry(AppLanguage language, string key, out string value)
        => GetDictionary(language).TryGetValue(key, out value!);

    private static Dictionary<string, string> GetDictionary(AppLanguage language) => language switch
    {
        AppLanguage.English => English,
        AppLanguage.Chinese => Chinese,
        AppLanguage.Korean => Korean,
        AppLanguage.TraditionalChinese => TraditionalChinese,
        AppLanguage.Vietnamese => Vietnamese,
        AppLanguage.Spanish => Spanish,
        AppLanguage.German => German,
        AppLanguage.French => French,
        AppLanguage.Portuguese => Portuguese,
        AppLanguage.Italian => Italian,
        _ => Japanese
    };

    private static readonly Dictionary<string, string> Japanese = new()
    {
        ["Description"] = "同一 LAN 内のスマホと PC / サーバー間で、QR コードを使って双方向に直接ファイルをやり取りします（完全ローカル・アプリ不要）。",
        ["UsageLine"] = "使い方: eft [オプション]",
        ["HelpOptions"] = """
オプション:
  -p, --port <n>       待ち受けポート番号（既定: 5050、使用中なら +10 まで自動探索）
  -d, --dir <path>     スマホから受信したファイルの保存先フォルダー
  -s, --share <path>   スマホへ渡すファイルを置く共有フォルダー
      --ip <addr>      待ち受け URL / QR に使うローカル IPv4 アドレスを明示指定
  -l, --lang <code>    表示とスマホ画面の初期言語（ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it）
  -t, --timeout <min>  セッションの有効期限（分、1〜1440、既定: 30）
      --once           1回のファイル転送（受信または送信）完了後に自動終了
      --no-qr          QR コードを表示せず URL のみ出力（リダイレクト・パイプ時は自動有効）
      --qr-plain       QR コードの ANSI 色付けを無効化
      --no-color       テキストの ANSI カラー出力を無効化
  -q, --quiet          進捗ログと補足案内を抑え、重要情報と転送結果行のみ表示
  -v, --version        バージョン情報を表示
  -h, --help           このヘルプを表示
""",
        ["HelpExitCodes"] = """
終了コード:
  0  正常終了（Ctrl+C、または --once / セッション中に転送完了）
  1  転送が1件も成立せずセッション期限切れ
  2  コマンドライン引数またはパスの指定誤り
  3  サーバー起動失敗などの実行時エラー
""",
        ["HelpExamples"] = """
使用例:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang en
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "ポート",
        ["LabelSession"] = "セッション",
        ["LabelReceiveDir"] = "受信先",
        ["LabelShareDir"] = "共有元",
        ["LabelOtherIps"] = "他の IP 候補",
        ["HintVirtualNicIp"] = "スマホから接続できない場合は、別の候補を --ip <addr> で指定してください。",
        ["NoteCustomIp"] = "※ 指定された IP は検出候補外です。スマホから到達可能なアドレスかご確認ください。",
        ["NotePortAutoChangedFmt"] = "（{0} が使用中のため自動変更）",
        ["SharedFileCountFmt"] = "（{0} 件のファイル）",
        ["SessionInfoFmt"] = "{0}（1:1 IP固定・有効期限 {1}分 / {2} まで）",
        ["NoteOnceMode"] = "モード: --once（最初のファイル転送完了後に自動終了します）",
        ["WaitingPrompt"] = "スマホで QR コードをスキャンしてください（1つの画面で送受信とも利用可能）。停止: Ctrl+C",
        ["EventConnectedFmt"] = "接続: {0}",
        ["EventReceivingFmt"] = "受信開始: {0}",
        ["EventReceivedFmt"] = "受信完了: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "送信完了: {0}",
        ["EventRejectedFmt"] = "アクセス拒否 ({0}): {1}",
        ["ReasonInvalidToken"] = "無効なトークン",
        ["ReasonExpired"] = "期限切れ",
        ["ReasonIpMismatch"] = "別端末からのアクセス (1:1 保護)",
        ["EventErrorFmt"] = "エラー: {0}",
        ["NoticeOnceCompleted"] = "転送が完了したため終了します（--once）。",
        ["NoticeStopped"] = "サーバーを停止しました。",
        ["NoticeExpiredNoTransferFmt"] = "セッションが {0} 分で期限切れになりました（転送は行われませんでした）。",
        ["NoticeExpiredCompletedFmt"] = "セッションが {0} 分で期限切れになりました（転送完了: {1} 件）。",
        ["ErrUnknownOptionFmt"] = "不明なオプションです: {0}",
        ["ErrUnexpectedArgFmt"] = "予期しない引数です: {0}",
        ["ErrNoSubcommandsHint"] = "eft には send / receive などのサブコマンドはありません。1つの QR コード・1つの画面で双方向転送できます。",
        ["ErrMissingValueFmt"] = "オプション {0} には値が必要です。",
        ["ErrInvalidPortFmt"] = "ポート番号 '{0}' が不正です。1〜65535 の整数を指定してください。",
        ["ErrInvalidTimeoutFmt"] = "タイムアウト '{0}' が不正です。1〜1440（分）の整数を指定してください。",
        ["ErrInvalidIpFmt"] = "IPv4 アドレス '{0}' が不正です。",
        ["ErrInvalidLangFmt"] = "未対応の言語コード '{0}' です。対応: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "使い方は 'eft --help' で確認できます。",
        ["ErrPathIsFileFmt"] = "指定されたパス '{0}' はフォルダーではなく既存のファイルです。",
        ["ErrShareNotFoundFmt"] = "指定された共有フォルダーが見つかりません: {0}",
        ["ErrCreateDirFmt"] = "フォルダー '{0}' の作成またはアクセスに失敗しました: {1}",
        ["ErrServerStartFmt"] = "転送サーバーを起動できませんでした: {0}"
    };

    private static readonly Dictionary<string, string> English = new()
    {
        ["Description"] = "Direct bidirectional file transfer between your phone and PC/server over local Wi-Fi via QR code (no cloud, no mobile app required).",
        ["UsageLine"] = "Usage: eft [options]",
        ["HelpOptions"] = """
Options:
  -p, --port <n>       Listening port (default: 5050; probes up to +10 if busy)
  -d, --dir <path>     Destination directory for files received from your phone
  -s, --share <path>   Shared directory containing files to send to your phone
      --ip <addr>      Explicitly specify local IPv4 address for the URL / QR code
  -l, --lang <code>    Initial UI & mobile language (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Session lifetime in minutes (1-1440, default: 30)
      --once           Exit automatically after one file transfer completes
      --no-qr          Do not render QR code; print URL only (auto-enabled when piped)
      --qr-plain       Disable ANSI foreground/background colors on QR code
      --no-color       Disable ANSI text colors
  -q, --quiet          Suppress progress logs and hints; show essential info and results only
  -v, --version        Show version information
  -h, --help           Show this help message
""",
        ["HelpExitCodes"] = """
Exit codes:
  0  Success (Ctrl+C, or transfer completed under --once / session lifetime)
  1  Session expired without any completed transfer
  2  Invalid arguments or directory path
  3  Runtime or server startup error
""",
        ["HelpExamples"] = """
Examples:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang en
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Port",
        ["LabelSession"] = "Session",
        ["LabelReceiveDir"] = "Receive",
        ["LabelShareDir"] = "Share",
        ["LabelOtherIps"] = "Other IPs",
        ["HintVirtualNicIp"] = "If your phone cannot connect, pass another candidate with --ip <addr>.",
        ["NoteCustomIp"] = "Note: Specified IP is not in the detected interface list; ensure it is reachable from your phone.",
        ["NotePortAutoChangedFmt"] = "(auto-selected because {0} was busy)",
        ["SharedFileCountFmt"] = "({0} file(s))",
        ["SessionInfoFmt"] = "{0} (1:1 IP lock, expires in {1} min, at {2})",
        ["NoteOnceMode"] = "Mode: --once (will exit automatically after the first completed transfer)",
        ["WaitingPrompt"] = "Scan the QR code with your phone (supports both upload & download in one page). Press Ctrl+C to stop.",
        ["EventConnectedFmt"] = "Connected: {0}",
        ["EventReceivingFmt"] = "Receiving: {0}",
        ["EventReceivedFmt"] = "Received: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Sent: {0}",
        ["EventRejectedFmt"] = "Rejected ({0}): {1}",
        ["ReasonInvalidToken"] = "invalid token",
        ["ReasonExpired"] = "expired",
        ["ReasonIpMismatch"] = "IP mismatch (1:1 lock)",
        ["EventErrorFmt"] = "Error: {0}",
        ["NoticeOnceCompleted"] = "Transfer completed; exiting (--once).",
        ["NoticeStopped"] = "Server stopped.",
        ["NoticeExpiredNoTransferFmt"] = "Session expired after {0} minute(s) with no completed transfers.",
        ["NoticeExpiredCompletedFmt"] = "Session expired after {0} minute(s) ({1} transfer(s) completed).",
        ["ErrUnknownOptionFmt"] = "Unknown option: {0}",
        ["ErrUnexpectedArgFmt"] = "Unexpected argument: {0}",
        ["ErrNoSubcommandsHint"] = "eft does not use subcommands like 'send' or 'receive'. One QR code and page supports both directions.",
        ["ErrMissingValueFmt"] = "Option {0} requires a value.",
        ["ErrInvalidPortFmt"] = "Invalid port '{0}'. Specify an integer between 1 and 65535.",
        ["ErrInvalidTimeoutFmt"] = "Invalid timeout '{0}'. Specify an integer between 1 and 1440 minutes.",
        ["ErrInvalidIpFmt"] = "Invalid IPv4 address '{0}'.",
        ["ErrInvalidLangFmt"] = "Unsupported language code '{0}'. Supported: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Run 'eft --help' for usage.",
        ["ErrPathIsFileFmt"] = "Path '{0}' points to an existing file, not a directory.",
        ["ErrShareNotFoundFmt"] = "Shared directory not found: {0}",
        ["ErrCreateDirFmt"] = "Failed to create or access directory '{0}': {1}",
        ["ErrServerStartFmt"] = "Failed to start transfer server: {0}"
    };

    private static readonly Dictionary<string, string> Chinese = new()
    {
        ["Description"] = "通过二维码在同一局域网内的手机与电脑/服务器之间双向直接传输文件（纯本地、无需安装手机应用）。",
        ["UsageLine"] = "用法: eft [选项]",
        ["HelpOptions"] = """
选项:
  -p, --port <n>       监听端口（默认: 5050，若占用则自动探测至 +10）
  -d, --dir <path>     保存从手机接收的文件的目录
  -s, --share <path>   存放要发送给手机的文件的共享目录
      --ip <addr>      显式指定用于 URL / 二维码的本地 IPv4 地址
  -l, --lang <code>    CLI 与手机页面初始语言（ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it）
  -t, --timeout <min>  会话有效期（分钟，1-1440，默认: 30）
      --once           完成一次文件传输后自动退出
      --no-qr          不绘制二维码，仅输出 URL（重定向或管道时自动启用）
      --qr-plain       禁用二维码的 ANSI 颜色
      --no-color       禁用文本 ANSI 颜色输出
  -q, --quiet          精简进度与提示，仅显示关键信息与传输结果行
  -v, --version        显示版本信息
  -h, --help           显示此帮助信息
""",
        ["HelpExitCodes"] = """
退出码:
  0  正常退出（Ctrl+C，或 --once / 会话期间完成传输）
  1  会话过期且未完成任何传输
  2  命令行参数或路径无效
  3  服务器启动失败等运行时错误
""",
        ["HelpExamples"] = """
示例:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang zh
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "端口",
        ["LabelSession"] = "会话",
        ["LabelReceiveDir"] = "接收目录",
        ["LabelShareDir"] = "共享目录",
        ["LabelOtherIps"] = "其他候选 IP",
        ["HintVirtualNicIp"] = "如果手机无法连接，请使用 --ip <addr> 指定其他候选地址。",
        ["NoteCustomIp"] = "注意: 指定的 IP 不在检测到的网卡列表中，请确认手机可访问该地址。",
        ["NotePortAutoChangedFmt"] = "（因 {0} 被占用而自动切换）",
        ["SharedFileCountFmt"] = "（{0} 个文件）",
        ["SessionInfoFmt"] = "{0}（1:1 IP 绑定，有效期 {1} 分钟 / 至 {2}）",
        ["NoteOnceMode"] = "模式: --once（首次文件传输完成后自动退出）",
        ["WaitingPrompt"] = "请用手机扫描二维码（同一页面支持双向收发）。按 Ctrl+C 停止。",
        ["EventConnectedFmt"] = "已连接: {0}",
        ["EventReceivingFmt"] = "开始接收: {0}",
        ["EventReceivedFmt"] = "接收完成: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "发送完成: {0}",
        ["EventRejectedFmt"] = "拒绝访问 ({0}): {1}",
        ["ReasonInvalidToken"] = "无效令牌",
        ["ReasonExpired"] = "已过期",
        ["ReasonIpMismatch"] = "IP 不匹配 (1:1 保护)",
        ["EventErrorFmt"] = "错误: {0}",
        ["NoticeOnceCompleted"] = "传输完成，正在退出（--once）。",
        ["NoticeStopped"] = "服务器已停止。",
        ["NoticeExpiredNoTransferFmt"] = "会话在 {0} 分钟后过期（未完成任何传输）。",
        ["NoticeExpiredCompletedFmt"] = "会话在 {0} 分钟后过期（已完成 {1} 次传输）。",
        ["ErrUnknownOptionFmt"] = "未知选项: {0}",
        ["ErrUnexpectedArgFmt"] = "意外参数: {0}",
        ["ErrNoSubcommandsHint"] = "eft 不使用 send / receive 子命令，单个二维码和页面即可支持双向传输。",
        ["ErrMissingValueFmt"] = "选项 {0} 需要参数值。",
        ["ErrInvalidPortFmt"] = "无效端口号 '{0}'，请指定 1 至 65535 之间的整数。",
        ["ErrInvalidTimeoutFmt"] = "无效超时 '{0}'，请指定 1 至 1440 分钟之间的整数。",
        ["ErrInvalidIpFmt"] = "无效的 IPv4 地址 '{0}'。",
        ["ErrInvalidLangFmt"] = "不支持的语言代码 '{0}'。支持: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "请运行 'eft --help' 查看用法。",
        ["ErrPathIsFileFmt"] = "路径 '{0}' 是已存在的文件而非目录。",
        ["ErrShareNotFoundFmt"] = "未找到指定的共享目录: {0}",
        ["ErrCreateDirFmt"] = "创建或访问目录 '{0}' 失败: {1}",
        ["ErrServerStartFmt"] = "无法启动传输服务器: {0}"
    };

    private static readonly Dictionary<string, string> TraditionalChinese = new()
    {
        ["Description"] = "透過 QR Code 在同一區域網路內的手機與電腦/伺服器之間雙向直接傳輸檔案（純本機、無需手機 App）。",
        ["UsageLine"] = "用法: eft [選項]",
        ["HelpOptions"] = """
選項:
  -p, --port <n>       監聽連接埠（預設: 5050，若被佔用則自動探測至 +10）
  -d, --dir <path>     儲存從手機接收檔案的資料夾
  -s, --share <path>   放置要傳送給手機檔案的分享資料夾
      --ip <addr>      明確指定用於 URL / QR Code 的本機 IPv4 位址
  -l, --lang <code>    CLI 與手機頁面初始語言（ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it）
  -t, --timeout <min>  連線階段有效時間（分鐘，1-1440，預設: 30）
      --once           完成一次檔案傳輸後自動結束
      --no-qr          不顯示 QR Code，僅輸出 URL（重導向或管線時自動啟用）
      --qr-plain       停用 QR Code 的 ANSI 色彩
      --no-color       停用文字 ANSI 色彩輸出
  -q, --quiet          省略進度與提示，僅顯示必要資訊與傳輸結果行
  -v, --version        顯示版本資訊
  -h, --help           顯示此說明
""",
        ["HelpExitCodes"] = """
結束代碼:
  0  正常結束（Ctrl+C，或於 --once / 連線期間完成傳輸）
  1  連線階段過期且未完成任何傳輸
  2  命令列參數或路徑無效
  3  伺服器啟動失敗等執行期錯誤
""",
        ["HelpExamples"] = """
範例:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang zh-TW
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "連接埠",
        ["LabelSession"] = "連線階段",
        ["LabelReceiveDir"] = "接收資料夾",
        ["LabelShareDir"] = "分享資料夾",
        ["LabelOtherIps"] = "其他候選 IP",
        ["HintVirtualNicIp"] = "若手機無法連線，請使用 --ip <addr> 指定其他候選位址。",
        ["NoteCustomIp"] = "注意: 指定的 IP 不在偵測到的網路介面清單中，請確認手機可連到此位址。",
        ["NotePortAutoChangedFmt"] = "（因 {0} 已被佔用而自動切換）",
        ["SharedFileCountFmt"] = "（{0} 個檔案）",
        ["SessionInfoFmt"] = "{0}（1:1 IP 鎖定，有效時間 {1} 分鐘 / 至 {2}）",
        ["NoteOnceMode"] = "模式: --once（首次檔案傳輸完成後自動結束）",
        ["WaitingPrompt"] = "請用手機掃描 QR Code（單一頁面即可雙向收發）。按 Ctrl+C 停止。",
        ["EventConnectedFmt"] = "已連線: {0}",
        ["EventReceivingFmt"] = "開始接收: {0}",
        ["EventReceivedFmt"] = "接收完成: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "傳送完成: {0}",
        ["EventRejectedFmt"] = "拒絕存取 ({0}): {1}",
        ["ReasonInvalidToken"] = "無效權杖",
        ["ReasonExpired"] = "已過期",
        ["ReasonIpMismatch"] = "IP 不符 (1:1 保護)",
        ["EventErrorFmt"] = "錯誤: {0}",
        ["NoticeOnceCompleted"] = "傳輸完成，即將結束（--once）。",
        ["NoticeStopped"] = "伺服器已停止。",
        ["NoticeExpiredNoTransferFmt"] = "連線階段已於 {0} 分鐘後過期（未完成任何傳輸）。",
        ["NoticeExpiredCompletedFmt"] = "連線階段已於 {0} 分鐘後過期（已完成 {1} 次傳輸）。",
        ["ErrUnknownOptionFmt"] = "未知選項: {0}",
        ["ErrUnexpectedArgFmt"] = "非預期的引數: {0}",
        ["ErrNoSubcommandsHint"] = "eft 不使用 send / receive 等子命令，單一 QR Code 與頁面即可支援雙向傳輸。",
        ["ErrMissingValueFmt"] = "選項 {0} 需要值。",
        ["ErrInvalidPortFmt"] = "連接埠 '{0}' 無效，請指定 1 至 65535 的整數。",
        ["ErrInvalidTimeoutFmt"] = "逾時 '{0}' 無效，請指定 1 至 1440 分鐘的整數。",
        ["ErrInvalidIpFmt"] = "IPv4 位址 '{0}' 無效。",
        ["ErrInvalidLangFmt"] = "不支援的語言代碼 '{0}'。支援: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "請執行 'eft --help' 查看用法。",
        ["ErrPathIsFileFmt"] = "路徑 '{0}' 為現有檔案而非資料夾。",
        ["ErrShareNotFoundFmt"] = "找不到指定的分享資料夾: {0}",
        ["ErrCreateDirFmt"] = "建立或存取資料夾 '{0}' 失敗: {1}",
        ["ErrServerStartFmt"] = "無法啟動傳輸伺服器: {0}"
    };

    private static readonly Dictionary<string, string> Korean = new()
    {
        ["Description"] = "같은 LAN 내 스마트폰과 PC/서버 간에 QR 코드로 양방향 파일을 직접 전송합니다 (완전 로컬, 모바일 앱 불필요).",
        ["UsageLine"] = "사용법: eft [옵션]",
        ["HelpOptions"] = """
옵션:
  -p, --port <n>       수신 대기 포트 (기본값: 5050, 사용 중이면 +10까지 자동 탐색)
  -d, --dir <path>     스마트폰에서 받은 파일을 저장할 폴더
  -s, --share <path>   스마트폰으로 보낼 파일이 있는 공유 폴더
      --ip <addr>      URL / QR 코드에 사용할 로컬 IPv4 주소 지정
  -l, --lang <code>    CLI 및 모바일 화면 초기 언어 (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  세션 유효 시간(분, 1-1440, 기본값: 30)
      --once           파일 전송 1회 완료 후 자동 종료
      --no-qr          QR 코드를 표시하지 않고 URL만 출력 (파이프 시 자동 적용)
      --qr-plain       QR 코드의 ANSI 색상 비활성화
      --no-color       텍스트 ANSI 색상 비활성화
  -q, --quiet          진행 로그와 안내를 숨기고 핵심 정보와 결과 줄만 출력
  -v, --version        버전 정보 표시
  -h, --help           도움말 표시
""",
        ["HelpExitCodes"] = """
종료 코드:
  0  정상 종료 (Ctrl+C, 또는 --once / 세션 중 전송 완료)
  1  전송 완료 없이 세션 만료
  2  잘못된 인수 또는 폴더 경로
  3  서버 시작 실패 등 런타임 오류
""",
        ["HelpExamples"] = """
예시:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang ko
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "포트",
        ["LabelSession"] = "세션",
        ["LabelReceiveDir"] = "수신 폴더",
        ["LabelShareDir"] = "공유 폴더",
        ["LabelOtherIps"] = "다른 IP 후보",
        ["HintVirtualNicIp"] = "스마트폰에서 연결되지 않으면 --ip <addr>로 다른 후보 주소를 지정하세요.",
        ["NoteCustomIp"] = "참고: 지정한 IP가 감지된 인터페이스 목록에 없습니다. 스마트폰에서 접속 가능한지 확인하세요.",
        ["NotePortAutoChangedFmt"] = "({0} 포트가 사용 중이어서 자동 변경됨)",
        ["SharedFileCountFmt"] = "(파일 {0}개)",
        ["SessionInfoFmt"] = "{0} (1:1 IP 고정, {1}분 후 만료 / {2}까지)",
        ["NoteOnceMode"] = "모드: --once (첫 번째 파일 전송 완료 후 자동 종료)",
        ["WaitingPrompt"] = "스마트폰으로 QR 코드를 스캔하세요 (한 화면에서 송수신 모두 가능). 중지: Ctrl+C",
        ["EventConnectedFmt"] = "연결됨: {0}",
        ["EventReceivingFmt"] = "수신 시작: {0}",
        ["EventReceivedFmt"] = "수신 완료: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "전송 완료: {0}",
        ["EventRejectedFmt"] = "접근 거부 ({0}): {1}",
        ["ReasonInvalidToken"] = "유효하지 않은 토큰",
        ["ReasonExpired"] = "만료됨",
        ["ReasonIpMismatch"] = "IP 불일치 (1:1 보호)",
        ["EventErrorFmt"] = "오류: {0}",
        ["NoticeOnceCompleted"] = "전송이 완료되어 종료합니다 (--once).",
        ["NoticeStopped"] = "서버가 중지되었습니다.",
        ["NoticeExpiredNoTransferFmt"] = "{0}분 후 세션이 만료되었습니다 (완료된 전송 없음).",
        ["NoticeExpiredCompletedFmt"] = "{0}분 후 세션이 만료되었습니다 (전송 완료: {1}건).",
        ["ErrUnknownOptionFmt"] = "알 수 없는 옵션: {0}",
        ["ErrUnexpectedArgFmt"] = "예상치 못한 인수: {0}",
        ["ErrNoSubcommandsHint"] = "eft는 send / receive 같은 하위 명령을 사용하지 않습니다. 하나의 QR과 화면으로 양방향 전송이 가능합니다.",
        ["ErrMissingValueFmt"] = "옵션 {0}에는 값이 필요합니다.",
        ["ErrInvalidPortFmt"] = "잘못된 포트 번호 '{0}'입니다. 1~65535 사이의 정수를 지정하세요.",
        ["ErrInvalidTimeoutFmt"] = "잘못된 제한 시간 '{0}'입니다. 1~1440 사이의 분 단위 정수를 지정하세요.",
        ["ErrInvalidIpFmt"] = "잘못된 IPv4 주소 '{0}'입니다.",
        ["ErrInvalidLangFmt"] = "지원하지 않는 언어 코드 '{0}'입니다. 지원: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "사용법은 'eft --help'를 실행하여 확인하세요.",
        ["ErrPathIsFileFmt"] = "경로 '{0}'은(는) 폴더가 아니라 기존 파일입니다.",
        ["ErrShareNotFoundFmt"] = "지정한 공유 폴더를 찾을 수 없습니다: {0}",
        ["ErrCreateDirFmt"] = "폴더 '{0}' 생성 또는 접근 실패: {1}",
        ["ErrServerStartFmt"] = "전송 서버를 시작할 수 없습니다: {0}"
    };

    private static readonly Dictionary<string, string> Vietnamese = new()
    {
        ["Description"] = "Truyền tệp hai chiều trực tiếp giữa điện thoại và PC/máy chủ qua mã QR trong mạng LAN nội bộ (không qua đám mây, không cần cài app).",
        ["UsageLine"] = "Cách dùng: eft [tùy chọn]",
        ["HelpOptions"] = """
Tùy chọn:
  -p, --port <n>       Cổng lắng nghe (mặc định: 5050; tự tìm tới +10 nếu bận)
  -d, --dir <path>     Thư mục lưu tệp nhận từ điện thoại
  -s, --share <path>   Thư mục chia sẻ chứa tệp gửi sang điện thoại
      --ip <addr>      Chỉ định địa chỉ IPv4 cục bộ cho URL / mã QR
  -l, --lang <code>    Ngôn ngữ mặc định (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Thời hạn phiên tính bằng phút (1-1440, mặc định: 30)
      --once           Tự động thoát sau khi hoàn tất 1 lần truyền tệp
      --no-qr          Không vẽ mã QR, chỉ in URL (tự bật khi chuyển hướng)
      --qr-plain       Tắt màu ANSI cho mã QR
      --no-color       Tắt màu ANSI cho văn bản
  -q, --quiet          Ẩn nhật ký tiến trình và gợi ý; chỉ hiện thông tin chính và kết quả
  -v, --version        Hiển thị thông tin phiên bản
  -h, --help           Hiển thị trợ giúp này
""",
        ["HelpExitCodes"] = """
Mã thoát:
  0  Thành công (Ctrl+C hoặc hoàn tất truyền với --once / trong phiên)
  1  Hết hạn phiên mà không có lần truyền nào thành công
  2  Tham số hoặc đường dẫn không hợp lệ
  3  Lỗi thực thi hoặc không khởi động được máy chủ
""",
        ["HelpExamples"] = """
Ví dụ:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang vi
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Cổng",
        ["LabelSession"] = "Phiên",
        ["LabelReceiveDir"] = "Thư mục nhận",
        ["LabelShareDir"] = "Thư mục chia sẻ",
        ["LabelOtherIps"] = "IP khác",
        ["HintVirtualNicIp"] = "Nếu điện thoại không kết nối được, hãy thử chỉ định địa chỉ khác bằng --ip <addr>.",
        ["NoteCustomIp"] = "Lưu ý: IP đã chỉ định không nằm trong danh sách giao diện mạng phát hiện được.",
        ["NotePortAutoChangedFmt"] = "(tự đổi vì cổng {0} đang bận)",
        ["SharedFileCountFmt"] = "({0} tệp)",
        ["SessionInfoFmt"] = "{0} (khóa IP 1:1, hết hạn sau {1} phút lúc {2})",
        ["NoteOnceMode"] = "Chế độ: --once (tự thoát sau khi hoàn tất lần truyền đầu tiên)",
        ["WaitingPrompt"] = "Quét mã QR bằng điện thoại (hỗ trợ cả gửi và nhận trên cùng một trang). Nhấn Ctrl+C để dừng.",
        ["EventConnectedFmt"] = "Đã kết nối: {0}",
        ["EventReceivingFmt"] = "Đang nhận: {0}",
        ["EventReceivedFmt"] = "Đã nhận: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Đã gửi: {0}",
        ["EventRejectedFmt"] = "Bị từ chối ({0}): {1}",
        ["ReasonInvalidToken"] = "token không hợp lệ",
        ["ReasonExpired"] = "đã hết hạn",
        ["ReasonIpMismatch"] = "sai khác IP (bảo vệ 1:1)",
        ["EventErrorFmt"] = "Lỗi: {0}",
        ["NoticeOnceCompleted"] = "Đã truyền xong; đang thoát (--once).",
        ["NoticeStopped"] = "Đã dừng máy chủ.",
        ["NoticeExpiredNoTransferFmt"] = "Phiên đã hết hạn sau {0} phút mà không có tệp nào được truyền.",
        ["NoticeExpiredCompletedFmt"] = "Phiên đã hết hạn sau {0} phút (đã truyền {1} tệp).",
        ["ErrUnknownOptionFmt"] = "Tùy chọn không xác định: {0}",
        ["ErrUnexpectedArgFmt"] = "Tham số không mong đợi: {0}",
        ["ErrNoSubcommandsHint"] = "eft không dùng lệnh con như 'send' hay 'receive'. Một mã QR và một trang hỗ trợ cả hai chiều.",
        ["ErrMissingValueFmt"] = "Tùy chọn {0} cần một giá trị.",
        ["ErrInvalidPortFmt"] = "Cổng '{0}' không hợp lệ. Nhập số nguyên từ 1 đến 65535.",
        ["ErrInvalidTimeoutFmt"] = "Thời gian chờ '{0}' không hợp lệ. Nhập số phút từ 1 đến 1440.",
        ["ErrInvalidIpFmt"] = "Địa chỉ IPv4 '{0}' không hợp lệ.",
        ["ErrInvalidLangFmt"] = "Mã ngôn ngữ '{0}' không được hỗ trợ. Hỗ trợ: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Chạy 'eft --help' để xem hướng dẫn.",
        ["ErrPathIsFileFmt"] = "Đường dẫn '{0}' là một tệp hiện có, không phải thư mục.",
        ["ErrShareNotFoundFmt"] = "Không tìm thấy thư mục chia sẻ: {0}",
        ["ErrCreateDirFmt"] = "Không thể tạo hoặc truy cập thư mục '{0}': {1}",
        ["ErrServerStartFmt"] = "Không thể khởi động máy chủ truyền tệp: {0}"
    };

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["Description"] = "Transferencia bidireccional directa de archivos entre el teléfono y la PC/servidor por código QR en la red LAN local (sin nube ni app móvil).",
        ["UsageLine"] = "Uso: eft [opciones]",
        ["HelpOptions"] = """
Opciones:
  -p, --port <n>       Puerto de escucha (predeterminado: 5050; prueba hasta +10 si está ocupado)
  -d, --dir <path>     Carpeta de destino para archivos recibidos del teléfono
  -s, --share <path>   Carpeta compartida con archivos para enviar al teléfono
      --ip <addr>      Especificar dirección IPv4 local para la URL / código QR
  -l, --lang <code>    Idioma inicial (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Duración de la sesión en minutos (1-1440, predeterminado: 30)
      --once           Salir automáticamente al completar una transferencia
      --no-qr          No mostrar código QR; imprimir solo la URL (automático al redirigir)
      --qr-plain       Desactivar colores ANSI del código QR
      --no-color       Desactivar colores ANSI del texto
  -q, --quiet          Ocultar progreso y avisos; mostrar solo datos esenciales y resultados
  -v, --version        Mostrar versión
  -h, --help           Mostrar esta ayuda
""",
        ["HelpExitCodes"] = """
Códigos de salida:
  0  Éxito (Ctrl+C o transferencia completada con --once / durante la sesión)
  1  Sesión expirada sin ninguna transferencia completada
  2  Argumentos o ruta inválidos
  3  Error de ejecución o al iniciar el servidor
""",
        ["HelpExamples"] = """
Ejemplos:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang es
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Puerto",
        ["LabelSession"] = "Sesión",
        ["LabelReceiveDir"] = "Recepción",
        ["LabelShareDir"] = "Compartido",
        ["LabelOtherIps"] = "Otras IPs",
        ["HintVirtualNicIp"] = "Si el teléfono no puede conectar, especifique otra dirección con --ip <addr>.",
        ["NoteCustomIp"] = "Nota: La IP indicada no está en las interfaces detectadas; verifique que sea accesible desde el teléfono.",
        ["NotePortAutoChangedFmt"] = "(cambiado automáticamente porque {0} estaba en uso)",
        ["SharedFileCountFmt"] = "({0} archivo(s))",
        ["SessionInfoFmt"] = "{0} (bloqueo IP 1:1, expira en {1} min, a las {2})",
        ["NoteOnceMode"] = "Modo: --once (saldrá automáticamente tras la primera transferencia completada)",
        ["WaitingPrompt"] = "Escanee el código QR con su teléfono (envío y recepción en una sola página). Pulse Ctrl+C para detener.",
        ["EventConnectedFmt"] = "Conectado: {0}",
        ["EventReceivingFmt"] = "Recibiendo: {0}",
        ["EventReceivedFmt"] = "Recibido: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Enviado: {0}",
        ["EventRejectedFmt"] = "Rechazado ({0}): {1}",
        ["ReasonInvalidToken"] = "token inválido",
        ["ReasonExpired"] = "expirado",
        ["ReasonIpMismatch"] = "IP distinta (protección 1:1)",
        ["EventErrorFmt"] = "Error: {0}",
        ["NoticeOnceCompleted"] = "Transferencia completada; saliendo (--once).",
        ["NoticeStopped"] = "Servidor detenido.",
        ["NoticeExpiredNoTransferFmt"] = "La sesión expiró tras {0} minuto(s) sin transferencias completadas.",
        ["NoticeExpiredCompletedFmt"] = "La sesión expiró tras {0} minuto(s) ({1} transferencia(s) completada(s)).",
        ["ErrUnknownOptionFmt"] = "Opción desconocida: {0}",
        ["ErrUnexpectedArgFmt"] = "Argumento inesperado: {0}",
        ["ErrNoSubcommandsHint"] = "eft no usa subcomandos como 'send' o 'receive'. Un solo QR y página cubren ambas direcciones.",
        ["ErrMissingValueFmt"] = "La opción {0} requiere un valor.",
        ["ErrInvalidPortFmt"] = "Puerto inválido '{0}'. Indique un entero entre 1 y 65535.",
        ["ErrInvalidTimeoutFmt"] = "Tiempo límite inválido '{0}'. Indique minutos entre 1 y 1440.",
        ["ErrInvalidIpFmt"] = "Dirección IPv4 inválida '{0}'.",
        ["ErrInvalidLangFmt"] = "Idioma no compatible '{0}'. Compatibles: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Ejecute 'eft --help' para ver el uso.",
        ["ErrPathIsFileFmt"] = "La ruta '{0}' es un archivo existente, no una carpeta.",
        ["ErrShareNotFoundFmt"] = "No se encontró la carpeta compartida: {0}",
        ["ErrCreateDirFmt"] = "Error al crear o acceder a la carpeta '{0}': {1}",
        ["ErrServerStartFmt"] = "No se pudo iniciar el servidor de transferencia: {0}"
    };

    private static readonly Dictionary<string, string> German = new()
    {
        ["Description"] = "Direkte bidirektionale Dateiübertragung zwischen Smartphone und PC/Server per QR-Code im lokalen LAN (ohne Cloud, ohne Smartphone-App).",
        ["UsageLine"] = "Verwendung: eft [Optionen]",
        ["HelpOptions"] = """
Optionen:
  -p, --port <n>       Lauschport (Standard: 5050; prüft bis +10 falls belegt)
  -d, --dir <path>     Zielordner für vom Smartphone empfangene Dateien
  -s, --share <path>   Freigabeordner mit Dateien für das Smartphone
      --ip <addr>      Lokale IPv4-Adresse für URL / QR-Code explizit angeben
  -l, --lang <code>    Startsprache (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Sitzungsgültigkeit in Minuten (1-1440, Standard: 30)
      --once           Nach einer abgeschlossenen Übertragung automatisch beenden
      --no-qr          Keinen QR-Code zeichnen, nur URL ausgeben (bei Umleitung automatisch)
      --qr-plain       ANSI-Farben für den QR-Code deaktivieren
      --no-color       ANSI-Textfarben deaktivieren
  -q, --quiet          Fortschritt und Hinweise ausblenden; nur Basisinfos und Ergebnisse anzeigen
  -v, --version        Versionsinformation anzeigen
  -h, --help           Diese Hilfe anzeigen
""",
        ["HelpExitCodes"] = """
Exit-Codes:
  0  Erfolg (Strg+C oder Übertragung mit --once / während der Sitzung abgeschlossen)
  1  Sitzung abgelaufen ohne abgeschlossene Übertragung
  2  Ungültige Argumente oder Pfadangaben
  3  Laufzeit- oder Serverstartfehler
""",
        ["HelpExamples"] = """
Beispiele:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang de
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Port",
        ["LabelSession"] = "Sitzung",
        ["LabelReceiveDir"] = "Empfang",
        ["LabelShareDir"] = "Freigabe",
        ["LabelOtherIps"] = "Weitere IPs",
        ["HintVirtualNicIp"] = "Falls keine Verbindung möglich ist, wählen Sie einen anderen Kandidaten mit --ip <addr>.",
        ["NoteCustomIp"] = "Hinweis: Die angegebene IP steht nicht in den erkannten Adaptern; Erreichbarkeit vom Smartphone prüfen.",
        ["NotePortAutoChangedFmt"] = "(automatisch gewählt, da {0} belegt war)",
        ["SharedFileCountFmt"] = "({0} Datei(en))",
        ["SessionInfoFmt"] = "{0} (1:1-IP-Sperre, gültig {1} Min., bis {2})",
        ["NoteOnceMode"] = "Modus: --once (beendet sich nach der ersten abgeschlossenen Übertragung)",
        ["WaitingPrompt"] = "QR-Code mit dem Smartphone scannen (Senden & Empfangen auf einer Seite). Stoppen mit Strg+C.",
        ["EventConnectedFmt"] = "Verbunden: {0}",
        ["EventReceivingFmt"] = "Empfange: {0}",
        ["EventReceivedFmt"] = "Empfangen: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Gesendet: {0}",
        ["EventRejectedFmt"] = "Abgelehnt ({0}): {1}",
        ["ReasonInvalidToken"] = "ungültiges Token",
        ["ReasonExpired"] = "abgelaufen",
        ["ReasonIpMismatch"] = "IP-Abweichung (1:1-Schutz)",
        ["EventErrorFmt"] = "Fehler: {0}",
        ["NoticeOnceCompleted"] = "Übertragung abgeschlossen; beende (--once).",
        ["NoticeStopped"] = "Server gestoppt.",
        ["NoticeExpiredNoTransferFmt"] = "Sitzung nach {0} Minute(n) ohne Übertragung abgelaufen.",
        ["NoticeExpiredCompletedFmt"] = "Sitzung nach {0} Minute(n) abgelaufen ({1} Übertragung(en) abgeschlossen).",
        ["ErrUnknownOptionFmt"] = "Unbekannte Option: {0}",
        ["ErrUnexpectedArgFmt"] = "Unerwartetes Argument: {0}",
        ["ErrNoSubcommandsHint"] = "eft verwendet keine Unterbefehle wie 'send' oder 'receive'. Ein QR-Code deckt beide Richtungen ab.",
        ["ErrMissingValueFmt"] = "Option {0} erfordert einen Wert.",
        ["ErrInvalidPortFmt"] = "Ungültiger Port '{0}'. Bitte eine Ganzzahl zwischen 1 und 65535 angeben.",
        ["ErrInvalidTimeoutFmt"] = "Ungültiges Timeout '{0}'. Bitte Minuten zwischen 1 und 1440 angeben.",
        ["ErrInvalidIpFmt"] = "Ungültige IPv4-Adresse '{0}'.",
        ["ErrInvalidLangFmt"] = "Nicht unterstützter Sprachcode '{0}'. Unterstützt: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Führen Sie 'eft --help' aus.",
        ["ErrPathIsFileFmt"] = "Der Pfad '{0}' ist eine vorhandene Datei, kein Ordner.",
        ["ErrShareNotFoundFmt"] = "Freigabeordner nicht gefunden: {0}",
        ["ErrCreateDirFmt"] = "Ordner '{0}' konnte nicht erstellt oder geöffnet werden: {1}",
        ["ErrServerStartFmt"] = "Übertragungsserver konnte nicht gestartet werden: {0}"
    };

    private static readonly Dictionary<string, string> French = new()
    {
        ["Description"] = "Transfert bidirectionnel direct de fichiers entre votre smartphone et votre PC/serveur via code QR sur le réseau local (sans cloud ni application mobile).",
        ["UsageLine"] = "Utilisation : eft [options]",
        ["HelpOptions"] = """
Options :
  -p, --port <n>       Port d'écoute (défaut : 5050 ; essaie jusqu'à +10 si occupé)
  -d, --dir <path>     Dossier de destination pour les fichiers reçus du téléphone
  -s, --share <path>   Dossier partagé contenant les fichiers à envoyer au téléphone
      --ip <addr>      Spécifier l'adresse IPv4 locale pour l'URL / le QR code
  -l, --lang <code>    Langue initiale (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Durée de session en minutes (1-1440, défaut : 30)
      --once           Quitter automatiquement après un transfert réussi
      --no-qr          Ne pas afficher le QR code, afficher uniquement l'URL
      --qr-plain       Désactiver les couleurs ANSI du QR code
      --no-color       Désactiver les couleurs ANSI du texte
  -q, --quiet          Masquer la progression et les conseils ; n'afficher que l'essentiel
  -v, --version        Afficher la version
  -h, --help           Afficher cette aide
""",
        ["HelpExitCodes"] = """
Codes de sortie :
  0  Succès (Ctrl+C, ou transfert terminé avec --once / durant la session)
  1  Session expirée sans aucun transfert réussi
  2  Arguments ou chemin invalides
  3  Erreur d'exécution ou de démarrage du serveur
""",
        ["HelpExamples"] = """
Exemples :
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang fr
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Port",
        ["LabelSession"] = "Session",
        ["LabelReceiveDir"] = "Réception",
        ["LabelShareDir"] = "Partage",
        ["LabelOtherIps"] = "Autres IPs",
        ["HintVirtualNicIp"] = "Si le téléphone ne se connecte pas, indiquez une autre adresse avec --ip <addr>.",
        ["NoteCustomIp"] = "Note : L'IP indiquée ne figure pas parmi les interfaces détectées ; vérifiez sa joignabilité.",
        ["NotePortAutoChangedFmt"] = "(modifié automatiquement car {0} était occupé)",
        ["SharedFileCountFmt"] = "({0} fichier(s))",
        ["SessionInfoFmt"] = "{0} (verrouillage IP 1:1, expire dans {1} min, à {2})",
        ["NoteOnceMode"] = "Mode : --once (arrêt automatique après le premier transfert réussi)",
        ["WaitingPrompt"] = "Scannez le QR code avec votre téléphone (envoi et réception sur une seule page). Ctrl+C pour arrêter.",
        ["EventConnectedFmt"] = "Connecté : {0}",
        ["EventReceivingFmt"] = "Réception : {0}",
        ["EventReceivedFmt"] = "Reçu : {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Envoyé : {0}",
        ["EventRejectedFmt"] = "Refusé ({0}) : {1}",
        ["ReasonInvalidToken"] = "jeton invalide",
        ["ReasonExpired"] = "expiré",
        ["ReasonIpMismatch"] = "IP différente (protection 1:1)",
        ["EventErrorFmt"] = "Erreur : {0}",
        ["NoticeOnceCompleted"] = "Transfert terminé ; arrêt (--once).",
        ["NoticeStopped"] = "Serveur arrêté.",
        ["NoticeExpiredNoTransferFmt"] = "Session expirée après {0} minute(s) sans transfert terminé.",
        ["NoticeExpiredCompletedFmt"] = "Session expirée après {0} minute(s) ({1} transfert(s) terminé(s)).",
        ["ErrUnknownOptionFmt"] = "Option inconnue : {0}",
        ["ErrUnexpectedArgFmt"] = "Argument inattendu : {0}",
        ["ErrNoSubcommandsHint"] = "eft n'utilise pas de sous-commandes comme 'send' ou 'receive'. Un seul QR gère les deux sens.",
        ["ErrMissingValueFmt"] = "L'option {0} attend une valeur.",
        ["ErrInvalidPortFmt"] = "Port invalide '{0}'. Indiquez un entier entre 1 et 65535.",
        ["ErrInvalidTimeoutFmt"] = "Délai invalide '{0}'. Indiquez un entier entre 1 et 1440 minutes.",
        ["ErrInvalidIpFmt"] = "Adresse IPv4 invalide '{0}'.",
        ["ErrInvalidLangFmt"] = "Code langue non pris en charge '{0}'. Valides : ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Exécutez 'eft --help' pour l'aide.",
        ["ErrPathIsFileFmt"] = "Le chemin '{0}' correspond à un fichier existant, pas un dossier.",
        ["ErrShareNotFoundFmt"] = "Dossier partagé introuvable : {0}",
        ["ErrCreateDirFmt"] = "Impossible de créer ou d'accéder au dossier '{0}' : {1}",
        ["ErrServerStartFmt"] = "Échec du démarrage du serveur : {0}"
    };

    private static readonly Dictionary<string, string> Portuguese = new()
    {
        ["Description"] = "Transferência bidirecional direta de arquivos entre celular e PC/servidor via QR code na rede local (sem nuvem e sem app).",
        ["UsageLine"] = "Uso: eft [opções]",
        ["HelpOptions"] = """
Opções:
  -p, --port <n>       Porta de escuta (padrão: 5050; tenta até +10 se ocupada)
  -d, --dir <path>     Pasta de destino para arquivos recebidos do celular
  -s, --share <path>   Pasta compartilhada com arquivos para enviar ao celular
      --ip <addr>      Especificar endereço IPv4 local para URL / QR code
  -l, --lang <code>    Idioma inicial (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Validade da sessão em minutos (1-1440, padrão: 30)
      --once           Encerrar automaticamente após concluir uma transferência
      --no-qr          Não exibir QR code; mostrar apenas a URL
      --qr-plain       Desativar cores ANSI no QR code
      --no-color       Desativar cores ANSI no texto
  -q, --quiet          Suprimir progresso e dicas; mostrar apenas dados essenciais e resultados
  -v, --version        Mostrar versão
  -h, --help           Mostrar esta ajuda
""",
        ["HelpExitCodes"] = """
Códigos de saída:
  0  Sucesso (Ctrl+C ou transferência concluída com --once / durante a sessão)
  1  Sessão expirada sem nenhuma transferência concluída
  2  Argumentos ou caminho inválidos
  3  Erro de execução ou ao iniciar o servidor
""",
        ["HelpExamples"] = """
Exemplos:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang pt
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Porta",
        ["LabelSession"] = "Sessão",
        ["LabelReceiveDir"] = "Recebimento",
        ["LabelShareDir"] = "Compartilhado",
        ["LabelOtherIps"] = "Outros IPs",
        ["HintVirtualNicIp"] = "Se o celular não conectar, tente outro endereço com --ip <addr>.",
        ["NoteCustomIp"] = "Nota: O IP especificado não está nas interfaces detectadas; confirme se o celular o alcança.",
        ["NotePortAutoChangedFmt"] = "(alterada automaticamente porque {0} estava em uso)",
        ["SharedFileCountFmt"] = "({0} arquivo(s))",
        ["SessionInfoFmt"] = "{0} (trava de IP 1:1, expira em {1} min, às {2})",
        ["NoteOnceMode"] = "Modo: --once (encerrará automaticamente após a primeira transferência concluída)",
        ["WaitingPrompt"] = "Escaneie o QR code com seu celular (envio e recebimento em uma única página). Ctrl+C para parar.",
        ["EventConnectedFmt"] = "Conectado: {0}",
        ["EventReceivingFmt"] = "Recebendo: {0}",
        ["EventReceivedFmt"] = "Recebido: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Enviado: {0}",
        ["EventRejectedFmt"] = "Rejeitado ({0}): {1}",
        ["ReasonInvalidToken"] = "token inválido",
        ["ReasonExpired"] = "expirado",
        ["ReasonIpMismatch"] = "IP diferente (proteção 1:1)",
        ["EventErrorFmt"] = "Erro: {0}",
        ["NoticeOnceCompleted"] = "Transferência concluída; encerrando (--once).",
        ["NoticeStopped"] = "Servidor parado.",
        ["NoticeExpiredNoTransferFmt"] = "Sessão expirada após {0} minuto(s) sem transferências concluídas.",
        ["NoticeExpiredCompletedFmt"] = "Sessão expirada após {0} minuto(s) ({1} transferência(s) concluída(s)).",
        ["ErrUnknownOptionFmt"] = "Opção desconhecida: {0}",
        ["ErrUnexpectedArgFmt"] = "Argumento inesperado: {0}",
        ["ErrNoSubcommandsHint"] = "eft não usa subcomandos como 'send' ou 'receive'. Um único QR atende os dois sentidos.",
        ["ErrMissingValueFmt"] = "A opção {0} requer um valor.",
        ["ErrInvalidPortFmt"] = "Porta inválida '{0}'. Informe um inteiro entre 1 e 65535.",
        ["ErrInvalidTimeoutFmt"] = "Tempo limite inválido '{0}'. Informe minutos entre 1 e 1440.",
        ["ErrInvalidIpFmt"] = "Endereço IPv4 inválido '{0}'.",
        ["ErrInvalidLangFmt"] = "Código de idioma não suportado '{0}'. Suportados: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Execute 'eft --help' para ver o uso.",
        ["ErrPathIsFileFmt"] = "O caminho '{0}' é um arquivo existente, não uma pasta.",
        ["ErrShareNotFoundFmt"] = "Pasta compartilhada não encontrada: {0}",
        ["ErrCreateDirFmt"] = "Falha ao criar ou acessar a pasta '{0}': {1}",
        ["ErrServerStartFmt"] = "Falha ao iniciar o servidor de transferência: {0}"
    };

    private static readonly Dictionary<string, string> Italian = new()
    {
        ["Description"] = "Trasferimento file bidirezionale diretto tra smartphone e PC/server tramite codice QR sulla LAN locale (senza cloud e senza app).",
        ["UsageLine"] = "Uso: eft [opzioni]",
        ["HelpOptions"] = """
Opzioni:
  -p, --port <n>       Porta di ascolto (predefinita: 5050; prova fino a +10 se occupata)
  -d, --dir <path>     Cartella di destinazione per i file ricevuti dallo smartphone
  -s, --share <path>   Cartella condivisa con i file da inviare allo smartphone
      --ip <addr>      Specifica l'indirizzo IPv4 locale per URL / codice QR
  -l, --lang <code>    Lingua iniziale (ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it)
  -t, --timeout <min>  Durata sessione in minuti (1-1440, predefinito: 30)
      --once           Esci automaticamente dopo un trasferimento completato
      --no-qr          Non mostrare il QR code, stampa solo l'URL
      --qr-plain       Disabilita i colori ANSI del QR code
      --no-color       Disabilita i colori ANSI del testo
  -q, --quiet          Sopprimi progresso e suggerimenti; mostra solo info essenziali e risultati
  -v, --version        Mostra versione
  -h, --help           Mostra questa guida
""",
        ["HelpExitCodes"] = """
Codici di uscita:
  0  Successo (Ctrl+C o trasferimento completato con --once / nella sessione)
  1  Sessione scaduta senza alcun trasferimento completato
  2  Argomenti o percorso non validi
  3  Errore di esecuzione o avvio server
""",
        ["HelpExamples"] = """
Esempi:
  eft
  eft --dir ./incoming --share ./outbox
  eft --ip 192.168.1.50 --port 8080 --lang it
  eft --once --no-qr
""",
        ["LabelUrl"] = "URL",
        ["LabelIp"] = "IP",
        ["LabelPort"] = "Porta",
        ["LabelSession"] = "Sessione",
        ["LabelReceiveDir"] = "Ricezione",
        ["LabelShareDir"] = "Condivisa",
        ["LabelOtherIps"] = "Altri IP",
        ["HintVirtualNicIp"] = "Se lo smartphone non si connette, prova un altro indirizzo con --ip <addr>.",
        ["NoteCustomIp"] = "Nota: L'IP specificato non è tra le interfacce rilevate; verifica che sia raggiungibile dal telefono.",
        ["NotePortAutoChangedFmt"] = "(cambiata automaticamente perché {0} era occupata)",
        ["SharedFileCountFmt"] = "({0} file)",
        ["SessionInfoFmt"] = "{0} (blocco IP 1:1, scade tra {1} min, alle {2})",
        ["NoteOnceMode"] = "Modalità: --once (uscita automatica dopo il primo trasferimento completato)",
        ["WaitingPrompt"] = "Scansiona il codice QR con lo smartphone (invio e ricezione in una sola pagina). Ctrl+C per fermare.",
        ["EventConnectedFmt"] = "Connesso: {0}",
        ["EventReceivingFmt"] = "Ricezione: {0}",
        ["EventReceivedFmt"] = "Ricevuto: {0} ({1}) -> {2}",
        ["EventSentFmt"] = "Inviato: {0}",
        ["EventRejectedFmt"] = "Rifiutato ({0}): {1}",
        ["ReasonInvalidToken"] = "token non valido",
        ["ReasonExpired"] = "scaduto",
        ["ReasonIpMismatch"] = "IP diverso (protezione 1:1)",
        ["EventErrorFmt"] = "Errore: {0}",
        ["NoticeOnceCompleted"] = "Trasferimento completato; uscita (--once).",
        ["NoticeStopped"] = "Server arrestato.",
        ["NoticeExpiredNoTransferFmt"] = "Sessione scaduta dopo {0} minuto/i senza trasferimenti completati.",
        ["NoticeExpiredCompletedFmt"] = "Sessione scaduta dopo {0} minuto/i ({1} trasferimento/i completato/i).",
        ["ErrUnknownOptionFmt"] = "Opzione sconosciuta: {0}",
        ["ErrUnexpectedArgFmt"] = "Argomento inatteso: {0}",
        ["ErrNoSubcommandsHint"] = "eft non usa sottocomandi come 'send' o 'receive'. Un solo QR gestisce entrambe le direzioni.",
        ["ErrMissingValueFmt"] = "L'opzione {0} richiede un valore.",
        ["ErrInvalidPortFmt"] = "Porta non valida '{0}'. Specifica un intero tra 1 e 65535.",
        ["ErrInvalidTimeoutFmt"] = "Timeout non valido '{0}'. Specifica i minuti tra 1 e 1440.",
        ["ErrInvalidIpFmt"] = "Indirizzo IPv4 non valido '{0}'.",
        ["ErrInvalidLangFmt"] = "Codice lingua non supportato '{0}'. Supportati: ja, en, zh, zh-TW, ko, vi, es, de, fr, pt, it",
        ["ErrSeeHelp"] = "Esegui 'eft --help' per vedere l'uso.",
        ["ErrPathIsFileFmt"] = "Il percorso '{0}' è un file esistente, non una cartella.",
        ["ErrShareNotFoundFmt"] = "Cartella condivisa non trovata: {0}",
        ["ErrCreateDirFmt"] = "Impossibile creare o accedere alla cartella '{0}': {1}",
        ["ErrServerStartFmt"] = "Impossibile avviare il server di trasferimento: {0}"
    };
}
