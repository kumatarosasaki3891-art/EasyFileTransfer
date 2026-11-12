using System.ComponentModel;

namespace EasyFileTransfer.Services;

/// <summary>
/// アプリ内で切り替えられる11言語の一覧です。
/// </summary>
public enum AppLanguage
{
    Japanese,
    English,
    Chinese,
    Korean,
    TraditionalChinese,
    Vietnamese,
    Spanish,
    German,
    French,
    Portuguese,
    Italian
}

/// <summary>
/// 画面の文字を多言語で切り替えるためのサービスクラスです。
/// Windows版(MAUI)とLinux版(Avalonia)の両方で使い回せるように、設定の読み書きを外部から差し替えられるようにしています。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string LanguagePreference = "EasyFileTransferAppLanguage";

    public static Func<string, string, string>? ExternalPreferenceGetter { get; set; }
    public static Action<string, string>? ExternalPreferenceSetter { get; set; }

    /// <summary>
    /// シングルトンとしてアプリ全体で1つのインスタンスを共有しています
    /// </summary>
    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppLanguage _currentLanguage = AppLanguage.Japanese;

    /// <summary>
    /// 現在選択中の言語です。変更すると設定を保存して画面側に通知を送ります。
    /// </summary>
    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                SaveLanguagePreference(value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            }
        }
    }

    public string this[string key] => Get(key);

    /// <summary>
    /// コンストラクタ：起動時に前回保存した言語（なければOSの言語）を読み込みます。
    /// </summary>
    public LocalizationService()
    {
        _currentLanguage = LoadInitialLanguage();
    }

    /// <summary>
    /// キー名に対応する翻訳テキストを取得します。見つからないときは日本語にフォールバックします。
    /// </summary>
    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var dict = GetDictionary(_currentLanguage);
        if (dict.TryGetValue(key, out var text)) return text;

        if (_currentLanguage != AppLanguage.Japanese &&
            JapaneseDictionary.TryGetValue(key, out var fallbackText))
        {
            return fallbackText;
        }

        return key;
    }

    /// <summary>
    /// {0} などの差し込み文字を含んだ翻訳テキストを整形して返します。
    /// </summary>
    public string Format(string key, params object[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch
        {
            return template;
        }
    }

    /// <summary>
    /// 現在の言語を "ja" や "en" などの言語コード文字列に変換して返します（スマホ側Web画面にも渡します）。
    /// </summary>
    public string GetCurrentLanguageCode() => _currentLanguage switch
    {
        AppLanguage.English => "en",
        AppLanguage.Chinese => "zh",
        AppLanguage.Korean => "ko",
        AppLanguage.TraditionalChinese => "zh-TW",
        AppLanguage.Vietnamese => "vi",
        AppLanguage.Spanish => "es",
        AppLanguage.German => "de",
        AppLanguage.French => "fr",
        AppLanguage.Portuguese => "pt",
        AppLanguage.Italian => "it",
        _ => "ja"
    };

    /// <summary>
    /// 言語コード（"ja", "en" など）から言語を切り替えます。
    /// </summary>
    public void SetLanguageByCode(string code)
    {
        CurrentLanguage = (code ?? string.Empty).ToLowerInvariant() switch
        {
            "en" => AppLanguage.English,
            "zh-tw" or "zh-hant" or "zh-hk" => AppLanguage.TraditionalChinese,
            "zh" or "zh-hans" or "zh-cn" => AppLanguage.Chinese,
            "ko" => AppLanguage.Korean,
            "vi" or "vie" => AppLanguage.Vietnamese,
            "es" or "spa" => AppLanguage.Spanish,
            "de" or "deu" or "ger" => AppLanguage.German,
            "fr" or "fra" or "fre" => AppLanguage.French,
            "pt" or "por" => AppLanguage.Portuguese,
            "it" or "ita" => AppLanguage.Italian,
            _ => AppLanguage.Japanese
        };
    }

    /// <summary>
    /// 初期言語を決める処理です。保存された設定 → OSのUI言語 → 日本語の順で決めています。
    /// </summary>
    private static AppLanguage LoadInitialLanguage()
    {
        try
        {
            string saved = string.Empty;
            if (ExternalPreferenceGetter != null)
            {
                saved = ExternalPreferenceGetter(LanguagePreference, string.Empty);
            }
#if !AVALONIA
            else
            {
                saved = Preferences.Default.Get(LanguagePreference, string.Empty);
            }
#endif
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved.ToLowerInvariant() switch
                {
                    "en" => AppLanguage.English,
                    "zh" or "zh-cn" or "zh-hans" => AppLanguage.Chinese,
                    "ko" => AppLanguage.Korean,
                    "zh-tw" or "zh-hant" => AppLanguage.TraditionalChinese,
                    "vi" or "vie" => AppLanguage.Vietnamese,
                    "es" => AppLanguage.Spanish,
                    "de" => AppLanguage.German,
                    "fr" => AppLanguage.French,
                    "pt" => AppLanguage.Portuguese,
                    "it" => AppLanguage.Italian,
                    _ => AppLanguage.Japanese
                };
            }
        }
        catch { }

        // OSの言語設定から推測
        var currentUiCulture = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
        if (currentUiCulture.StartsWith("ja")) return AppLanguage.Japanese;
        if (currentUiCulture.StartsWith("zh-tw") || currentUiCulture.StartsWith("zh-hk") || currentUiCulture.StartsWith("zh-hant")) return AppLanguage.TraditionalChinese;
        if (currentUiCulture.StartsWith("zh")) return AppLanguage.Chinese;
        if (currentUiCulture.StartsWith("ko")) return AppLanguage.Korean;
        if (currentUiCulture.StartsWith("vi")) return AppLanguage.Vietnamese;
        if (currentUiCulture.StartsWith("es")) return AppLanguage.Spanish;
        if (currentUiCulture.StartsWith("de")) return AppLanguage.German;
        if (currentUiCulture.StartsWith("fr")) return AppLanguage.French;
        if (currentUiCulture.StartsWith("pt")) return AppLanguage.Portuguese;
        if (currentUiCulture.StartsWith("it")) return AppLanguage.Italian;
        if (currentUiCulture.StartsWith("en")) return AppLanguage.English;

        return AppLanguage.Japanese;
    }

    /// <summary>
    /// 選んだ言語を次回起動時にも引き継げるように保存します。
    /// </summary>
    private static void SaveLanguagePreference(AppLanguage language)
    {
        try
        {
            var code = language switch
            {
                AppLanguage.English => "en",
                AppLanguage.Chinese => "zh",
                AppLanguage.Korean => "ko",
                AppLanguage.TraditionalChinese => "zh-TW",
                AppLanguage.Vietnamese => "vi",
                AppLanguage.Spanish => "es",
                AppLanguage.German => "de",
                AppLanguage.French => "fr",
                AppLanguage.Portuguese => "pt",
                AppLanguage.Italian => "it",
                _ => "ja"
            };

            if (ExternalPreferenceSetter != null)
            {
                ExternalPreferenceSetter(LanguagePreference, code);
            }
#if !AVALONIA
            else
            {
                Preferences.Default.Set(LanguagePreference, code);
            }
#endif
        }
        catch { }
    }

    /// <summary>
    /// 指定された言語に対応する辞書（Dictionary）を選びます。
    /// </summary>
    private static Dictionary<string, string> GetDictionary(AppLanguage language) => language switch
    {
        AppLanguage.English => EnglishDictionary,
        AppLanguage.Chinese => ChineseDictionary,
        AppLanguage.Korean => KoreanDictionary,
        AppLanguage.TraditionalChinese => TraditionalChineseDictionary,
        AppLanguage.Vietnamese => VietnameseDictionary,
        AppLanguage.Spanish => SpanishDictionary,
        AppLanguage.German => GermanDictionary,
        AppLanguage.French => FrenchDictionary,
        AppLanguage.Portuguese => PortugueseDictionary,
        AppLanguage.Italian => ItalianDictionary,
        _ => JapaneseDictionary
    };

    #region Dictionaries

    private static readonly Dictionary<string, string> JapaneseDictionary = new()
    {
        ["AppTitle"] = "簡単ファイル転送",
        ["AppSubtitle"] = "スマホとPC間でQRコードによる高速ローカル直接転送",
        ["LanguageLabel"] = "言語:",
        ["TabUpload"] = "📤 受信 (スマホ → PC)",
        ["TabDownload"] = "📥 送信 (PC → スマホ)",
        ["QrCodeTitle"] = "📱 スマホのカメラでQRコードを読み取ってください",
        ["QrCodeTitleUpload"] = "📱 スマホでスキャンしてPCへファイルを送信",
        ["QrCodeTitleDownload"] = "📱 スマホでスキャンしてPCからファイルをダウンロード",
        ["QrCodeHint"] = "スマホのカメラでスキャンすると送信画面が開き、即座にPCへ転送できます。",
        ["QrCodeHintUpload"] = "標準のカメラでスキャンすると、ファイル送信画面が開きます。",
        ["QrCodeHintDownload"] = "標準のカメラでスキャンすると、ファイルダウンロード画面が開きます。",
        ["TransferUrlLabel"] = "転送用URL:",
        ["CopyUrlButton"] = "📋 URLコピー",
        ["UrlCopiedToast"] = "URLをクリップボードにコピーしました",
        ["SwitchIpButton"] = "🔄 IP切替 ({0}/{1})",
        ["SessionLabel"] = "セッション:",
        ["RegenerateSessionButton"] = "🔄 QR更新",
        ["SessionProtectedBadge"] = "🔒 1:1 保護",
        ["ServerStatusTitle"] = "転送状況:",
        ["StatusWaiting"] = "🟢 待機中（スマホからの接続を待っています）",
        ["StatusConnected"] = "🔵 スマホ接続中（Web画面が開かれました）",
        ["StatusReceiving"] = "🔄 ファイルを受信中...",
        ["StatusCompleted"] = "✅ ファイルを受信して保存しました！",
        ["StatusDownloading"] = "⬇️ ファイルをダウンロード送信中...",
        ["StatusDownloadCompleted"] = "✅ ファイルのダウンロードが完了しました！",
        ["StatusError"] = "⚠️ エラーが発生しました",
        ["TargetFolderTitle"] = "📁 保存先フォルダー（スマホからの受信先）",
        ["DownloadFolderTitle"] = "📁 送信用フォルダー（スマホへ共有するPCフォルダー）",
        ["ChangeFolderButton"] = "フォルダー変更",
        ["OpenFolderButton"] = "フォルダーを開く",
        ["FolderNotFound"] = "指定されたフォルダーが見つかりません。",
        ["ReceivedFilesTitle"] = "📋 受信したファイル一覧",
        ["NoReceivedFiles"] = "まだ受信したファイルはありません。スマホから送信してください。",
        ["SharedFilesTitle"] = "📂 共有中のファイル一覧（PC側）",
        ["NoSharedFiles"] = "送信用フォルダーにファイルがありません。「＋ファイルを追加」またはフォルダーに配置してください。",
        ["AddFilesButton"] = "＋ ファイルを追加",
        ["RefreshListButton"] = "🔄 更新",
        ["ClearListButton"] = "履歴クリア",
        ["OpenFileButton"] = "開く",
        ["DeleteButton"] = "削除",
        ["SavedBadge"] = "✓ 保存済",
        ["SelectFilesToShareTitle"] = "スマホへ共有するファイルを選択",
        ["InstructionsTitle"] = "💡 スマホ → PC 受信の使い方",
        ["InstructionStep1"] = "1. PCとスマホを同じWi-Fi（同一ネットワーク）に接続します。",
        ["InstructionStep2"] = "2. スマホの標準カメラで左のQRコードをスキャンします。",
        ["InstructionStep3"] = "3. 開いたWeb画面で画像・PDF・CSV・GPX・Zip等を選んで送信すると、PCに即時保存されます！",
        ["DownloadInstructionsTitle"] = "💡 PC → スマホ 送信（ダウンロード）の使い方",
        ["DownloadStep1"] = "1. 送信用フォルダーに、スマホへ送りたいファイルを追加します。",
        ["DownloadStep2"] = "2. スマホの標準カメラで左のQRコードをスキャンします。",
        ["DownloadStep3"] = "3. 開いたWeb画面でファイル一覧からタップすると、スマホへ直接ダウンロードされます！",
        ["InstructionsNote"] = "🔒 インターネットを経由せず、同一Wi-Fi内だけで安全・高速に直接転送されます。",
        ["NetworkTroubleHint"] = "※ 繋がらない場合は「IP切替」を試すか、ルーターの「プライバシーセパレーター（端末間通信遮断）」が無効かご確認ください。",
        ["SupportedFormatsLabel"] = "対応ファイル: 画像 (JPEG, PNG, HEIC, GIF等), PDF, JSON, CSV, GPX, ZIP, その他全般",
        ["NoNetworkError"] = "有効なWi-FiまたはLAN接続が見つかりません。ネットワーク接続をご確認ください。",
        ["ToolTipAbout"] = "ℹ️ アプリ情報",
        ["VersionLabel"] = "バージョン",
        ["AboutAppDescription"] = "スマホとPC間でQRコードにより双方向ファイルを直接転送するツール\nローカルネットワーク内で安心・安全・高速にファイル転送します。",
        ["HomepageLabel"] = "ホームページ",
        ["CloseButton"] = "閉じる"
    };

    private static readonly Dictionary<string, string> EnglishDictionary = new()
    {
        ["AppTitle"] = "EasyFileTransfer",
        ["AppSubtitle"] = "High-speed local direct file transfer between Phone and PC via QR Code",
        ["LanguageLabel"] = "Language:",
        ["TabUpload"] = "📤 Receive (Phone → PC)",
        ["TabDownload"] = "📥 Send (PC → Phone)",
        ["QrCodeTitle"] = "📱 Scan QR code with your phone camera",
        ["QrCodeTitleUpload"] = "📱 Scan with phone to send files to PC",
        ["QrCodeTitleDownload"] = "📱 Scan with phone to download files from PC",
        ["QrCodeHint"] = "Scan with your camera to open the web uploader and transfer directly to your PC.",
        ["QrCodeHintUpload"] = "Scan with your camera to open the uploader page.",
        ["QrCodeHintDownload"] = "Scan with your camera to open the downloader page.",
        ["TransferUrlLabel"] = "Transfer URL:",
        ["CopyUrlButton"] = "📋 Copy URL",
        ["UrlCopiedToast"] = "URL copied to clipboard",
        ["SwitchIpButton"] = "🔄 Switch IP ({0}/{1})",
        ["SessionLabel"] = "Session:",
        ["RegenerateSessionButton"] = "🔄 Refresh QR",
        ["SessionProtectedBadge"] = "🔒 1:1 Protected",
        ["ServerStatusTitle"] = "Transfer Status:",
        ["StatusWaiting"] = "🟢 Waiting (ready for connection)",
        ["StatusConnected"] = "🔵 Phone connected (web page opened)",
        ["StatusReceiving"] = "🔄 Receiving files...",
        ["StatusCompleted"] = "✅ File received and saved successfully!",
        ["StatusDownloading"] = "⬇️ Sending file to phone...",
        ["StatusDownloadCompleted"] = "✅ File download completed!",
        ["StatusError"] = "⚠️ An error occurred",
        ["TargetFolderTitle"] = "📁 Destination Folder (Incoming Files)",
        ["DownloadFolderTitle"] = "📁 Outgoing Folder (Shared to Phone)",
        ["ChangeFolderButton"] = "Change Folder",
        ["OpenFolderButton"] = "Open Folder",
        ["FolderNotFound"] = "Folder not found.",
        ["ReceivedFilesTitle"] = "📋 Received Files",
        ["NoReceivedFiles"] = "No files received yet. Send files from your phone.",
        ["SharedFilesTitle"] = "📂 Shared Files (PC)",
        ["NoSharedFiles"] = "No files in the outgoing folder. Click '+ Add Files' or drop files into the folder.",
        ["AddFilesButton"] = "＋ Add Files",
        ["RefreshListButton"] = "🔄 Refresh",
        ["ClearListButton"] = "Clear History",
        ["OpenFileButton"] = "Open",
        ["DeleteButton"] = "Delete",
        ["SavedBadge"] = "✓ Saved",
        ["SelectFilesToShareTitle"] = "Select files to share with phone",
        ["InstructionsTitle"] = "💡 How to Receive (Phone → PC)",
        ["InstructionStep1"] = "1. Connect your PC and phone to the same Wi-Fi network.",
        ["InstructionStep2"] = "2. Scan the QR code on the left with your phone's camera.",
        ["InstructionStep3"] = "3. Select photos, PDFs, CSV, GPX, Zip or any files on your phone to transfer immediately!",
        ["DownloadInstructionsTitle"] = "💡 How to Send (PC → Phone)",
        ["DownloadStep1"] = "1. Put the files you want to send to your phone into the outgoing folder.",
        ["DownloadStep2"] = "2. Scan the QR code on the left with your phone camera.",
        ["DownloadStep3"] = "3. Tap files on the opened web page to download them directly to your phone!",
        ["InstructionsNote"] = "🔒 Files are transferred directly over your local Wi-Fi without going through the internet.",
        ["NetworkTroubleHint"] = "* If you cannot connect, try 'Switch IP' or check if 'AP Isolation / Privacy Separator' is disabled on your router.",
        ["SupportedFormatsLabel"] = "Supported: Photos (JPEG, PNG, HEIC, etc.), PDF, JSON, CSV, GPX, ZIP, and all other files",
        ["NoNetworkError"] = "No active network connection (Wi-Fi or LAN) found. Please check your PC connection.",
        ["ToolTipAbout"] = "ℹ️ About App",
        ["VersionLabel"] = "Version",
        ["AboutAppDescription"] = "Direct bidirectional file transfer tool between phone and PC via QR code.\nFast, safe, and secure file transfer within your local network.",
        ["HomepageLabel"] = "Website",
        ["CloseButton"] = "Close"
    };

    private static readonly Dictionary<string, string> ChineseDictionary = new()
    {
        ["AppTitle"] = "简易文件传输",
        ["AppSubtitle"] = "通过二维码在手机与电脑间进行高速局域网直接传输",
        ["LanguageLabel"] = "语言:",
        ["TabUpload"] = "📤 接收 (手机 → 电脑)",
        ["TabDownload"] = "📥 发送 (电脑 → 手机)",
        ["QrCodeTitle"] = "📱 请使用手机相机扫描二维码",
        ["QrCodeTitleUpload"] = "📱 手机扫码向电脑发送文件",
        ["QrCodeTitleDownload"] = "📱 手机扫码从电脑下载文件",
        ["QrCodeHint"] = "使用手机相机扫码即可打开上传页面，即时将文件传输到电脑。",
        ["QrCodeHintUpload"] = "使用手机相机扫码即可打开文件发送页面。",
        ["QrCodeHintDownload"] = "使用手机相机扫码即可打开文件下载页面。",
        ["TransferUrlLabel"] = "传输网址:",
        ["CopyUrlButton"] = "📋 复制网址",
        ["UrlCopiedToast"] = "网址已复制到剪贴板",
        ["SwitchIpButton"] = "🔄 切换 IP ({0}/{1})",
        ["SessionLabel"] = "会话:",
        ["RegenerateSessionButton"] = "🔄 刷新二维码",
        ["SessionProtectedBadge"] = "🔒 1:1 保护",
        ["ServerStatusTitle"] = "传输状态:",
        ["StatusWaiting"] = "🟢 正在等待（等待手机连接）",
        ["StatusConnected"] = "🔵 手机已连接（已打开页面）",
        ["StatusReceiving"] = "🔄 正在接收文件...",
        ["StatusCompleted"] = "✅ 文件已成功接收并保存！",
        ["StatusDownloading"] = "⬇️ 正在向手机发送文件...",
        ["StatusDownloadCompleted"] = "✅ 文件下载完成！",
        ["StatusError"] = "⚠️ 发生错误",
        ["TargetFolderTitle"] = "📁 保存目标文件夹（接收手机文件）",
        ["DownloadFolderTitle"] = "📁 发送目标文件夹（共享给手机的文件）",
        ["ChangeFolderButton"] = "更改文件夹",
        ["OpenFolderButton"] = "打开文件夹",
        ["FolderNotFound"] = "找不到指定的文件夹。",
        ["ReceivedFilesTitle"] = "📋 已接收文件列表",
        ["NoReceivedFiles"] = "尚无已接收的文件。请从手机发送文件。",
        ["SharedFilesTitle"] = "📂 正在共享的文件（电脑端）",
        ["NoSharedFiles"] = "共享文件夹中暂无文件。请点击“+ 添加文件”或直接放入文件。",
        ["AddFilesButton"] = "＋ 添加文件",
        ["RefreshListButton"] = "🔄 刷新",
        ["ClearListButton"] = "清空历史",
        ["OpenFileButton"] = "打开",
        ["DeleteButton"] = "删除",
        ["SavedBadge"] = "✓ 已保存",
        ["SelectFilesToShareTitle"] = "选择要共享给手机的文件",
        ["InstructionsTitle"] = "💡 手机 → 电脑 接收指南",
        ["InstructionStep1"] = "1. 将电脑和手机连接到同一个 Wi-Fi 网络。",
        ["InstructionStep2"] = "2. 用手机相机扫描左侧的二维码。",
        ["InstructionStep3"] = "3. 在手机页面中选择图片、PDF、CSV、GPX、Zip 等文件并发送，立即保存到电脑！",
        ["DownloadInstructionsTitle"] = "💡 电脑 → 手机 发送指南",
        ["DownloadStep1"] = "1. 将想要发送到手机的文件放入发送文件夹中。",
        ["DownloadStep2"] = "2. 用手机相机扫描左侧的二维码。",
        ["DownloadStep3"] = "3. 在手机打开的网页中点击文件，即可直接下载到手机！",
        ["InstructionsNote"] = "🔒 无需经过互联网，仅在同一局域网 Wi-Fi 内安全、高速直接传输。",
        ["NetworkTroubleHint"] = "※ 如无法连接，请尝试“切换 IP”或检查路由器是否开启了“AP 隔离 / 隐私隔离”。",
        ["SupportedFormatsLabel"] = "支持格式: 图片 (JPEG, PNG, HEIC等), PDF, JSON, CSV, GPX, ZIP 及各种通用文件",
        ["NoNetworkError"] = "未找到有效的局域网（Wi-Fi 或有线连接）。请检查电脑网络设置。",
        ["ToolTipAbout"] = "ℹ️ 关于应用",
        ["VersionLabel"] = "版本",
        ["AboutAppDescription"] = "通过QR码在手机与电脑间双向直接传输文件的工具\n在局域网内安全、快速地传输文件。",
        ["HomepageLabel"] = "主页",
        ["CloseButton"] = "关闭"
    };

    private static readonly Dictionary<string, string> KoreanDictionary = new()
    {
        ["AppTitle"] = "간편 파일 전송",
        ["AppSubtitle"] = "QR 코드로 스마트폰과 PC 간 초고속 로컬 직접 파일 전송",
        ["LanguageLabel"] = "언어:",
        ["TabUpload"] = "📤 수신 (스마트폰 → PC)",
        ["TabDownload"] = "📥 전송 (PC → 스마트폰)",
        ["QrCodeTitle"] = "📱 스마트폰 카메라로 QR 코드를 스캔하세요",
        ["QrCodeTitleUpload"] = "📱 스마트폰으로 스캔하여 PC로 파일 보내기",
        ["QrCodeTitleDownload"] = "📱 스마트폰으로 스캔하여 PC에서 파일 다운로드",
        ["QrCodeHint"] = "스마트폰 카메라로 스캔하면 전송 화면이 열려 즉시 PC로 전송할 수 있습니다.",
        ["QrCodeHintUpload"] = "스마트폰 카메라로 스캔하면 파일 전송 화면이 열립니다.",
        ["QrCodeHintDownload"] = "스마트폰 카메라로 스캔하면 파일 다운로드 화면이 열립니다.",
        ["TransferUrlLabel"] = "전송 URL:",
        ["CopyUrlButton"] = "📋 URL 복사",
        ["UrlCopiedToast"] = "URL이 클립보드에 복사되었습니다",
        ["SwitchIpButton"] = "🔄 IP 전환 ({0}/{1})",
        ["SessionLabel"] = "세션:",
        ["RegenerateSessionButton"] = "🔄 QR 갱신",
        ["SessionProtectedBadge"] = "🔒 1:1 보호",
        ["ServerStatusTitle"] = "전송 상태:",
        ["StatusWaiting"] = "🟢 대기 중 (스마트폰 연결 대기)",
        ["StatusConnected"] = "🔵 스마트폰 연결됨 (웹 화면 열림)",
        ["StatusReceiving"] = "🔄 파일 수신 중...",
        ["StatusCompleted"] = "✅ 파일을 수신하여 저장했습니다!",
        ["StatusDownloading"] = "⬇️ 스마트폰으로 파일 다운로드 전송 중...",
        ["StatusDownloadCompleted"] = "✅ 파일 다운로드가 완료되었습니다!",
        ["StatusError"] = "⚠️ 오류가 발생했습니다",
        ["TargetFolderTitle"] = "📁 저장 폴더 (스마트폰에서 수신)",
        ["DownloadFolderTitle"] = "📁 전송 폴더 (스마트폰에 공유할 PC 폴더)",
        ["ChangeFolderButton"] = "폴더 변경",
        ["OpenFolderButton"] = "폴더 열기",
        ["FolderNotFound"] = "지정된 폴더를 찾을 수 없습니다.",
        ["ReceivedFilesTitle"] = "📋 수신된 파일 목록",
        ["NoReceivedFiles"] = "아직 수신된 파일이 없습니다. 스마트폰에서 파일을 보내보세요.",
        ["SharedFilesTitle"] = "📂 공유 중인 파일 목록 (PC)",
        ["NoSharedFiles"] = "전송 폴더에 파일이 없습니다. '+ 파일 추가'를 누르거나 폴더에 파일을 넣으세요.",
        ["AddFilesButton"] = "＋ 파일 추가",
        ["RefreshListButton"] = "🔄 새로고침",
        ["ClearListButton"] = "기록 삭제",
        ["OpenFileButton"] = "열기",
        ["DeleteButton"] = "삭제",
        ["SavedBadge"] = "✓ 저장됨",
        ["SelectFilesToShareTitle"] = "스마트폰에 공유할 파일 선택",
        ["InstructionsTitle"] = "💡 스마트폰 → PC 수신 방법",
        ["InstructionStep1"] = "1. PC와 스마트폰을 동일한 Wi-Fi 네트워크에 연결합니다.",
        ["InstructionStep2"] = "2. 스마트폰 카메라로 왼쪽의 QR 코드를 스캔합니다.",
        ["InstructionStep3"] = "3. 모바일 화면에서 사진, PDF, CSV, GPX, Zip 등을 선택해 전송하면 PC에 즉시 저장됩니다!",
        ["DownloadInstructionsTitle"] = "💡 PC → 스마트폰 전송 (다운로드) 방법",
        ["DownloadStep1"] = "1. 스마트폰으로 보낼 파일을 전송 폴더에 추가합니다.",
        ["DownloadStep2"] = "2. 스마트폰 카메라로 왼쪽의 QR 코드를 스캔합니다.",
        ["DownloadStep3"] = "3. 열린 웹 화면에서 파일을 탭하면 스마트폰으로 직접 다운로드됩니다!",
        ["InstructionsNote"] = "🔒 인터넷을 거치지 않고 로컬 Wi-Fi 내에서만 안전하고 빠르게 직접 전송됩니다.",
        ["NetworkTroubleHint"] = "※ 연결되지 않는 경우 'IP 전환'을 시도하거나 공유기의 'AP 격리 / 프라이버시 세포레이터' 해제 여부를 확인하세요.",
        ["SupportedFormatsLabel"] = "지원 형식: 이미지 (JPEG, PNG, HEIC 등), PDF, JSON, CSV, GPX, ZIP 및 모든 파일",
        ["NoNetworkError"] = "활성 Wi-Fi 또는 LAN 연결을 찾을 수 없습니다. PC 네트워크를 확인하세요.",
        ["ToolTipAbout"] = "ℹ️ 앱 정보",
        ["VersionLabel"] = "버전",
        ["AboutAppDescription"] = "QR 코드로 스마트폰과 PC 간 양방향 직접 파일 전송 도구\n로컬 네트워크 내에서 안전하고 빠르게 파일을 전송합니다.",
        ["HomepageLabel"] = "홈페이지",
        ["CloseButton"] = "닫기"
    };

    private static readonly Dictionary<string, string> TraditionalChineseDictionary = new()
    {
        ["AppTitle"] = "簡易檔案傳輸",
        ["AppSubtitle"] = "透過 QR Code 在手機與電腦間高速區域網路直接傳輸",
        ["LanguageLabel"] = "語言:",
        ["TabUpload"] = "📤 接收 (手機 → 電腦)",
        ["TabDownload"] = "📥 傳送 (電腦 → 手機)",
        ["QrCodeTitle"] = "📱 請使用手機相機掃描 QR Code",
        ["QrCodeTitleUpload"] = "📱 手機掃描向電腦傳送檔案",
        ["QrCodeTitleDownload"] = "📱 手機掃描從電腦下載檔案",
        ["QrCodeHint"] = "使用手機相機掃描即可開啟上傳頁面，即時將檔案傳輸至電腦。",
        ["QrCodeHintUpload"] = "使用手機相機掃描即可開啟檔案傳送頁面。",
        ["QrCodeHintDownload"] = "使用手機相機掃描即可開啟檔案下載頁面。",
        ["TransferUrlLabel"] = "傳輸網址:",
        ["CopyUrlButton"] = "📋 複製網址",
        ["UrlCopiedToast"] = "網址已複製到剪貼簿",
        ["SwitchIpButton"] = "🔄 切換 IP ({0}/{1})",
        ["SessionLabel"] = "連線階段:",
        ["RegenerateSessionButton"] = "🔄 重新整理 QR",
        ["SessionProtectedBadge"] = "🔒 1:1 保護",
        ["ServerStatusTitle"] = "傳輸狀態:",
        ["StatusWaiting"] = "🟢 等待中（等待手機連線）",
        ["StatusConnected"] = "🔵 手機已連線（已開啟網頁）",
        ["StatusReceiving"] = "🔄 正在接收檔案...",
        ["StatusCompleted"] = "✅ 檔案已成功接收並儲存！",
        ["StatusDownloading"] = "⬇️ 正在向手機傳送檔案...",
        ["StatusDownloadCompleted"] = "✅ 檔案下載完成！",
        ["StatusError"] = "⚠️ 發生錯誤",
        ["TargetFolderTitle"] = "📁 儲存目標資料夾（接收手機檔案）",
        ["DownloadFolderTitle"] = "📁 傳送目標資料夾（分享給手機的檔案）",
        ["ChangeFolderButton"] = "變更資料夾",
        ["OpenFolderButton"] = "開啟資料夾",
        ["FolderNotFound"] = "找不到指定的資料夾。",
        ["ReceivedFilesTitle"] = "📋 已接收檔案清單",
        ["NoReceivedFiles"] = "尚未接收任何檔案。請從手機發送檔案。",
        ["SharedFilesTitle"] = "📂 正在分享的檔案（電腦端）",
        ["NoSharedFiles"] = "分享資料夾中尚無檔案。請點選「+ 新增檔案」或將檔案放入資料夾。",
        ["AddFilesButton"] = "＋ 新增檔案",
        ["RefreshListButton"] = "🔄 重新整理",
        ["ClearListButton"] = "清除紀錄",
        ["OpenFileButton"] = "開啟",
        ["DeleteButton"] = "刪除",
        ["SavedBadge"] = "✓ 已儲存",
        ["SelectFilesToShareTitle"] = "選取要分享至手機的檔案",
        ["InstructionsTitle"] = "💡 手機 → 電腦 接收說明",
        ["InstructionStep1"] = "1. 將電腦與手機連接至同一個 Wi-Fi 網路。",
        ["InstructionStep2"] = "2. 使用手機相機掃描左側的 QR Code。",
        ["InstructionStep3"] = "3. 在手機頁面選取相片、PDF、CSV、GPX、Zip 等檔案並傳送，即時儲存至電腦！",
        ["DownloadInstructionsTitle"] = "💡 電腦 → 手機 傳送 (下載) 說明",
        ["DownloadStep1"] = "1. 將想傳送給手機的檔案新增到傳送資料夾中。",
        ["DownloadStep2"] = "2. 使用手機相機掃描左側的 QR Code。",
        ["DownloadStep3"] = "3. 在手機開啟的網頁點選檔案，即可直接下載至手機！",
        ["InstructionsNote"] = "🔒 無須經過網際網路，僅在同一個區域 Wi-Fi 內安全、高速直接傳輸。",
        ["NetworkTroubleHint"] = "※ 若無法連線，請嘗試「切換 IP」或確認路由器是否啟用了「AP 隔離 / 隱私隔離」。",
        ["SupportedFormatsLabel"] = "支援格式: 圖片 (JPEG, PNG, HEIC等), PDF, JSON, CSV, GPX, ZIP 及各類通用檔案",
        ["NoNetworkError"] = "未找到有效的區域網路（Wi-Fi 或有線連接）。請檢查電腦網路設定。",
        ["ToolTipAbout"] = "ℹ️ 關於應用",
        ["VersionLabel"] = "版本",
        ["AboutAppDescription"] = "透過QR Code在手機與電腦間雙向直接傳輸檔案的工具\n在區域網路內安全、快速地傳輸檔案。",
        ["HomepageLabel"] = "官方網站",
        ["CloseButton"] = "關閉"
    };

    private static readonly Dictionary<string, string> VietnameseDictionary = new()
    {
        ["AppTitle"] = "EasyFileTransfer",
        ["AppSubtitle"] = "Truyền tệp trực tiếp tốc độ cao giữa Điện thoại và PC qua mã QR trong mạng cục bộ",
        ["LanguageLabel"] = "Ngôn ngữ:",
        ["TabUpload"] = "📤 Nhận (Điện thoại → PC)",
        ["TabDownload"] = "📥 Gửi (PC → Điện thoại)",
        ["QrCodeTitle"] = "📱 Quét mã QR bằng camera điện thoại của bạn",
        ["QrCodeTitleUpload"] = "📱 Quét để gửi tệp từ điện thoại sang PC",
        ["QrCodeTitleDownload"] = "📱 Quét để tải tệp từ PC về điện thoại",
        ["QrCodeHint"] = "Quét bằng camera để mở trang web tải lên và chuyển thẳng vào máy tính của bạn.",
        ["QrCodeHintUpload"] = "Quét bằng camera để mở trang gửi tệp.",
        ["QrCodeHintDownload"] = "Quét bằng camera để mở trang tải tệp.",
        ["TransferUrlLabel"] = "URL truyền:",
        ["CopyUrlButton"] = "📋 Sao chép URL",
        ["UrlCopiedToast"] = "Đã sao chép URL vào khay nhớ tạm",
        ["SwitchIpButton"] = "🔄 Đổi IP ({0}/{1})",
        ["SessionLabel"] = "Phiên:",
        ["RegenerateSessionButton"] = "🔄 Làm mới QR",
        ["SessionProtectedBadge"] = "🔒 Bảo vệ 1:1",
        ["ServerStatusTitle"] = "Trạng thái:",
        ["StatusWaiting"] = "🟢 Đang chờ (sẵn sàng kết nối)",
        ["StatusConnected"] = "🔵 Điện thoại đã kết nối (trang web đã mở)",
        ["StatusReceiving"] = "🔄 Đang nhận tệp...",
        ["StatusCompleted"] = "✅ Tệp đã được nhận và lưu thành công!",
        ["StatusDownloading"] = "⬇️ Đang gửi tệp sang điện thoại...",
        ["StatusDownloadCompleted"] = "✅ Đã tải tệp thành công!",
        ["StatusError"] = "⚠️ Đã xảy ra lỗi",
        ["TargetFolderTitle"] = "📁 Thư mục lưu trữ (Tệp nhận từ điện thoại)",
        ["DownloadFolderTitle"] = "📁 Thư mục gửi đi (Chia sẻ sang điện thoại)",
        ["ChangeFolderButton"] = "Đổi thư mục",
        ["OpenFolderButton"] = "Mở thư mục",
        ["FolderNotFound"] = "Không tìm thấy thư mục.",
        ["ReceivedFilesTitle"] = "📋 Danh sách tệp đã nhận",
        ["NoReceivedFiles"] = "Chưa có tệp nào. Vui lòng gửi từ điện thoại.",
        ["SharedFilesTitle"] = "📂 Tệp đang chia sẻ (PC)",
        ["NoSharedFiles"] = "Không có tệp nào trong thư mục gửi đi. Bấm '+ Thêm tệp' để bắt đầu.",
        ["AddFilesButton"] = "＋ Thêm tệp",
        ["RefreshListButton"] = "🔄 Làm mới",
        ["ClearListButton"] = "Xóa lịch sử",
        ["OpenFileButton"] = "Mở",
        ["DeleteButton"] = "Xóa",
        ["SavedBadge"] = "✓ Đã lưu",
        ["SelectFilesToShareTitle"] = "Chọn tệp để chia sẻ với điện thoại",
        ["InstructionsTitle"] = "💡 Hướng dẫn nhận (Điện thoại → PC)",
        ["InstructionStep1"] = "1. Kết nối PC và điện thoại vào cùng một mạng Wi-Fi.",
        ["InstructionStep2"] = "2. Quét mã QR bên trái bằng máy ảnh trên điện thoại.",
        ["InstructionStep3"] = "3. Chọn ảnh, PDF, CSV, GPX, Zip hoặc bất kỳ tệp nào để gửi ngay lập tức!",
        ["DownloadInstructionsTitle"] = "💡 Hướng dẫn gửi (PC → Điện thoại)",
        ["DownloadStep1"] = "1. Thêm các tệp bạn muốn gửi vào thư mục gửi đi.",
        ["DownloadStep2"] = "2. Dùng camera điện thoại quét mã QR bên trái.",
        ["DownloadStep3"] = "3. Chạm vào tệp trên trang web vừa mở để tải về điện thoại!",
        ["InstructionsNote"] = "🔒 Tệp được truyền trực tiếp qua mạng Wi-Fi nội bộ an toàn và tốc độ cao mà không qua internet.",
        ["NetworkTroubleHint"] = "* Nếu không kết nối được, hãy thử 'Đổi IP' hoặc kiểm tra tính năng cách ly AP trên bộ định tuyến.",
        ["SupportedFormatsLabel"] = "Định dạng hỗ trợ: Ảnh (JPEG, PNG, HEIC...), PDF, JSON, CSV, GPX, ZIP và tất cả các tệp khác",
        ["NoNetworkError"] = "Không tìm thấy kết nối Wi-Fi hoặc mạng LAN. Vui lòng kiểm tra kết nối mạng PC.",
        ["ToolTipAbout"] = "ℹ️ Giới thiệu ứng dụng",
        ["VersionLabel"] = "Phiên bản",
        ["AboutAppDescription"] = "Công cụ truyền tệp hai chiều trực tiếp giữa điện thoại và PC qua mã QR.\nTruyền tệp nhanh chóng và an toàn trong mạng nội bộ.",
        ["HomepageLabel"] = "Trang web",
        ["CloseButton"] = "Đóng"
    };

    private static readonly Dictionary<string, string> SpanishDictionary = new()
    {
        ["AppTitle"] = "Transferencia Fácil de Archivos",
        ["AppSubtitle"] = "Transferencia directa de alta velocidad entre el teléfono y la PC mediante código QR",
        ["LanguageLabel"] = "Idioma:",
        ["TabUpload"] = "📤 Recibir (Teléfono → PC)",
        ["TabDownload"] = "📥 Enviar (PC → Teléfono)",
        ["QrCodeTitle"] = "📱 Escanee el código QR con la cámara de su teléfono",
        ["QrCodeTitleUpload"] = "📱 Escanee para enviar archivos a la PC",
        ["QrCodeTitleDownload"] = "📱 Escanee para descargar archivos de la PC",
        ["QrCodeHint"] = "Escanee con la cámara para abrir la pantalla de envío y transferir archivos directamente a su PC.",
        ["QrCodeHintUpload"] = "Escanee con la cámara para abrir la página de envío de archivos.",
        ["QrCodeHintDownload"] = "Escanee con la cámara para abrir la página de descarga de archivos.",
        ["TransferUrlLabel"] = "URL de transferencia:",
        ["CopyUrlButton"] = "📋 Copiar URL",
        ["UrlCopiedToast"] = "URL copiada al portapapeles",
        ["SwitchIpButton"] = "🔄 Cambiar IP ({0}/{1})",
        ["SessionLabel"] = "Sesión:",
        ["RegenerateSessionButton"] = "🔄 Renovar QR",
        ["SessionProtectedBadge"] = "🔒 Protección 1:1",
        ["ServerStatusTitle"] = "Estado de transferencia:",
        ["StatusWaiting"] = "🟢 En espera (esperando conexión del teléfono)",
        ["StatusConnected"] = "🔵 Teléfono conectado (página web abierta)",
        ["StatusReceiving"] = "🔄 Recibiendo archivos...",
        ["StatusCompleted"] = "✅ ¡Archivo recibido y guardado con éxito!",
        ["StatusDownloading"] = "⬇️ Enviando archivo al teléfono...",
        ["StatusDownloadCompleted"] = "✅ ¡Descarga de archivo completada!",
        ["StatusError"] = "⚠️ Ocurrió un error",
        ["TargetFolderTitle"] = "📁 Carpeta de destino (Archivos recibidos)",
        ["DownloadFolderTitle"] = "📁 Carpeta de envío (Compartida con el teléfono)",
        ["ChangeFolderButton"] = "Cambiar carpeta",
        ["OpenFolderButton"] = "Abrir carpeta",
        ["FolderNotFound"] = "Carpeta no encontrada.",
        ["ReceivedFilesTitle"] = "📋 Archivos recibidos",
        ["NoReceivedFiles"] = "Aún no hay archivos recibidos. Envíe archivos desde su teléfono.",
        ["SharedFilesTitle"] = "📂 Archivos compartidos (PC)",
        ["NoSharedFiles"] = "No hay archivos en la carpeta de envío. Haga clic en '+ Añadir archivos'.",
        ["AddFilesButton"] = "＋ Añadir archivos",
        ["RefreshListButton"] = "🔄 Actualizar",
        ["ClearListButton"] = "Borrar historial",
        ["OpenFileButton"] = "Abrir",
        ["DeleteButton"] = "Eliminar",
        ["SavedBadge"] = "✓ Guardado",
        ["SelectFilesToShareTitle"] = "Seleccione archivos para compartir con el teléfono",
        ["InstructionsTitle"] = "💡 Cómo recibir (Teléfono → PC)",
        ["InstructionStep1"] = "1. Conecte su PC y teléfono a la misma red Wi-Fi.",
        ["InstructionStep2"] = "2. Escanee el código QR a la izquierda con la cámara de su teléfono.",
        ["InstructionStep3"] = "3. Seleccione fotos, PDF, CSV, GPX, Zip o cualquier archivo en su teléfono para transferirlos de inmediato.",
        ["DownloadInstructionsTitle"] = "💡 Cómo enviar (PC → Teléfono)",
        ["DownloadStep1"] = "1. Agregue los archivos que desea enviar a la carpeta de envío.",
        ["DownloadStep2"] = "2. Escanee el código QR de la izquierda con la cámara del teléfono.",
        ["DownloadStep3"] = "3. Toque los archivos en la página web abierta para descargarlos en el teléfono.",
        ["InstructionsNote"] = "🔒 Los archivos se transfieren de forma directa y segura a través de su Wi-Fi local sin pasar por Internet.",
        ["NetworkTroubleHint"] = "* Si no puede conectarse, intente 'Cambiar IP' o verifique si la 'Aislamiento de AP / Separador de privacidad' está desactivada en su router.",
        ["SupportedFormatsLabel"] = "Formatos compatibles: Fotos (JPEG, PNG, HEIC, etc.), PDF, JSON, CSV, GPX, ZIP y todos los demás archivos",
        ["NoNetworkError"] = "No se encontró conexión de red activa (Wi-Fi o LAN). Verifique la conexión de su PC.",
        ["ToolTipAbout"] = "ℹ️ Acerca de la aplicación",
        ["VersionLabel"] = "Versión",
        ["AboutAppDescription"] = "Herramienta de transferencia bidireccional de archivos entre el teléfono y la PC mediante código QR.\nTransferencia rápida y segura dentro de la red local.",
        ["HomepageLabel"] = "Sitio web",
        ["CloseButton"] = "Cerrar"
    };

    private static readonly Dictionary<string, string> GermanDictionary = new()
    {
        ["AppTitle"] = "Einfache Dateiübertragung",
        ["AppSubtitle"] = "Schnelle lokale Direktübertragung zwischen Smartphone und PC per QR-Code",
        ["LanguageLabel"] = "Sprache:",
        ["TabUpload"] = "📤 Empfangen (Handy → PC)",
        ["TabDownload"] = "📥 Senden (PC → Handy)",
        ["QrCodeTitle"] = "📱 QR-Code mit der Smartphone-Kamera scannen",
        ["QrCodeTitleUpload"] = "📱 Mit Smartphone scannen, um Dateien an PC zu senden",
        ["QrCodeTitleDownload"] = "📱 Mit Smartphone scannen, um Dateien vom PC herunterzuladen",
        ["QrCodeHint"] = "Mit der Kamera scannen, um die Upload-Seite zu öffnen und Dateien direkt auf den PC zu übertragen.",
        ["QrCodeHintUpload"] = "Mit der Kamera scannen, um die Seite zum Senden von Dateien zu öffnen.",
        ["QrCodeHintDownload"] = "Mit der Kamera scannen, um die Seite zum Herunterladen von Dateien zu öffnen.",
        ["TransferUrlLabel"] = "Übertragungs-URL:",
        ["CopyUrlButton"] = "📋 URL kopieren",
        ["UrlCopiedToast"] = "URL in die Zwischenablage kopiert",
        ["SwitchIpButton"] = "🔄 IP wechseln ({0}/{1})",
        ["SessionLabel"] = "Sitzung:",
        ["RegenerateSessionButton"] = "🔄 QR erneuern",
        ["SessionProtectedBadge"] = "🔒 1:1 Schutz",
        ["ServerStatusTitle"] = "Übertragungsstatus:",
        ["StatusWaiting"] = "🟢 Warten auf Verbindung (Smartphone bereit)",
        ["StatusConnected"] = "🔵 Smartphone verbunden (Webseite geöffnet)",
        ["StatusReceiving"] = "🔄 Dateien werden empfangen...",
        ["StatusCompleted"] = "✅ Datei empfangen und erfolgreich gespeichert!",
        ["StatusDownloading"] = "⬇️ Datei wird an Smartphone gesendet...",
        ["StatusDownloadCompleted"] = "✅ Datei-Download abgeschlossen!",
        ["StatusError"] = "⚠️ Ein Fehler ist aufgetreten",
        ["TargetFolderTitle"] = "📁 Zielordner (Vom Smartphone empfangen)",
        ["DownloadFolderTitle"] = "📁 Freigabeordner (Für Smartphone bereitgestellt)",
        ["ChangeFolderButton"] = "Ordner ändern",
        ["OpenFolderButton"] = "Ordner öffnen",
        ["FolderNotFound"] = "Ordner nicht gefunden.",
        ["ReceivedFilesTitle"] = "📋 Empfangene Dateien",
        ["NoReceivedFiles"] = "Noch keine Dateien empfangen. Senden Sie Dateien von Ihrem Smartphone.",
        ["SharedFilesTitle"] = "📂 Freigegebene Dateien (PC)",
        ["NoSharedFiles"] = "Keine Dateien im Freigabeordner. Klicken Sie auf '+ Dateien hinzufügen'.",
        ["AddFilesButton"] = "＋ Dateien hinzufügen",
        ["RefreshListButton"] = "🔄 Aktualisieren",
        ["ClearListButton"] = "Verlauf löschen",
        ["OpenFileButton"] = "Öffnen",
        ["DeleteButton"] = "Löschen",
        ["SavedBadge"] = "✓ Gespeichert",
        ["SelectFilesToShareTitle"] = "Dateien zum Freigeben für das Smartphone auswählen",
        ["InstructionsTitle"] = "💡 Empfangen (Handy → PC)",
        ["InstructionStep1"] = "1. Verbinden Sie Ihren PC und Ihr Smartphone mit demselben WLAN-Netzwerk.",
        ["InstructionStep2"] = "2. Scannen Sie den QR-Code links mit der Smartphone-Kamera.",
        ["InstructionStep3"] = "3. Wählen Sie Fotos, PDFs, CSV, GPX, ZIP oder andere Dateien aus, um sie sofort zu übertragen.",
        ["DownloadInstructionsTitle"] = "💡 Senden (PC → Handy)",
        ["DownloadStep1"] = "1. Fügen Sie die gewünschten Dateien in den Freigabeordner ein.",
        ["DownloadStep2"] = "2. Scannen Sie den QR-Code links mit der Handykamera.",
        ["DownloadStep3"] = "3. Tippen Sie auf der geöffneten Webseite auf Dateien, um sie herunterzuladen!",
        ["InstructionsNote"] = "🔒 Dateien werden sicher und schnell über das lokale WLAN übertragen, ohne das Internet zu nutzen.",
        ["NetworkTroubleHint"] = "* Wenn keine Verbindung hergestellt werden kann, versuchen Sie 'IP wechseln' oder prüfen Sie, ob die 'AP-Isolation' im Router deaktiviert ist.",
        ["SupportedFormatsLabel"] = "Unterstützte Formate: Fotos (JPEG, PNG, HEIC etc.), PDF, JSON, CSV, GPX, ZIP und alle weiteren Dateien",
        ["NoNetworkError"] = "Keine aktive Netzwerkverbindung (WLAN oder LAN) gefunden. Bitte prüfen Sie die PC-Netzwerkverbindung.",
        ["ToolTipAbout"] = "ℹ️ Über die App",
        ["VersionLabel"] = "Version",
        ["AboutAppDescription"] = "Direkte bidirektionale Dateiübertragung zwischen Smartphone und PC per QR-Code.\nSchnelle und sichere Übertragung im lokalen Netzwerk.",
        ["HomepageLabel"] = "Webseite",
        ["CloseButton"] = "Schließen"
    };

    private static readonly Dictionary<string, string> FrenchDictionary = new()
    {
        ["AppTitle"] = "Transfert Facile de Fichiers",
        ["AppSubtitle"] = "Transfert direct local ultra-rapide entre smartphone et PC via QR Code",
        ["LanguageLabel"] = "Langue :",
        ["TabUpload"] = "📤 Recevoir (Téléphone → PC)",
        ["TabDownload"] = "📥 Envoyer (PC → Téléphone)",
        ["QrCodeTitle"] = "📱 Scannez le code QR avec l'appareil photo du téléphone",
        ["QrCodeTitleUpload"] = "📱 Scannez pour envoyer des fichiers vers le PC",
        ["QrCodeTitleDownload"] = "📱 Scannez pour télécharger des fichiers depuis le PC",
        ["QrCodeHint"] = "Scannez avec votre appareil photo pour ouvrir la page d'envoi et transférer directement sur votre PC.",
        ["QrCodeHintUpload"] = "Scannez avec votre appareil photo pour ouvrir la page d'envoi de fichiers.",
        ["QrCodeHintDownload"] = "Scannez avec votre appareil photo pour ouvrir la page de téléchargement.",
        ["TransferUrlLabel"] = "URL de transfert :",
        ["CopyUrlButton"] = "📋 Copier l'URL",
        ["UrlCopiedToast"] = "URL copiée dans le presse-papiers",
        ["SwitchIpButton"] = "🔄 Changer d'IP ({0}/{1})" ,
        ["SessionLabel"] = "Session :",
        ["RegenerateSessionButton"] = "🔄 Actualiser le QR",
        ["SessionProtectedBadge"] = "🔒 Protection 1:1",
        ["ServerStatusTitle"] = "État du transfert :",
        ["StatusWaiting"] = "🟢 En attente (prêt pour la connexion)",
        ["StatusConnected"] = "🔵 Téléphone connecté (page web ouverte)",
        ["StatusReceiving"] = "🔄 Réception des fichiers...",
        ["StatusCompleted"] = "✅ Fichier reçu et enregistré avec succès !",
        ["StatusDownloading"] = "⬇️ Envoi du fichier vers le téléphone...",
        ["StatusDownloadCompleted"] = "✅ Téléchargement du fichier terminé !",
        ["StatusError"] = "⚠️ Une erreur est survenue",
        ["TargetFolderTitle"] = "📁 Dossier de destination (Fichiers reçus)",
        ["DownloadFolderTitle"] = "📁 Dossier d'envoi (Partagé avec le téléphone)",
        ["ChangeFolderButton"] = "Changer de dossier",
        ["OpenFolderButton"] = "Ouvrir le dossier",
        ["FolderNotFound"] = "Dossier introuvable.",
        ["ReceivedFilesTitle"] = "📋 Fichiers reçus",
        ["NoReceivedFiles"] = "Aucun fichier reçu pour l'instant. Envoyez des fichiers depuis votre téléphone.",
        ["SharedFilesTitle"] = "📂 Fichiers partagés (PC)",
        ["NoSharedFiles"] = "Aucun fichier dans le dossier d'envoi. Cliquez sur '+ Ajouter des fichiers'.",
        ["AddFilesButton"] = "＋ Ajouter des fichiers",
        ["RefreshListButton"] = "🔄 Actualiser",
        ["ClearListButton"] = "Effacer l'historique",
        ["OpenFileButton"] = "Ouvrir",
        ["DeleteButton"] = "Supprimer",
        ["SavedBadge"] = "✓ Enregistré",
        ["SelectFilesToShareTitle"] = "Sélectionner des fichiers à partager avec le téléphone",
        ["InstructionsTitle"] = "💡 Recevoir (Téléphone → PC)",
        ["InstructionStep1"] = "1. Connectez votre PC et votre téléphone au même réseau Wi-Fi.",
        ["InstructionStep2"] = "2. Scannez le code QR à gauche avec l'appareil photo de votre téléphone.",
        ["InstructionStep3"] = "3. Sélectionnez photos, PDF, CSV, GPX, ZIP ou tout fichier pour les transférer immédiatement.",
        ["DownloadInstructionsTitle"] = "💡 Envoyer (PC → Téléphone)",
        ["DownloadStep1"] = "1. Ajoutez les fichiers à transférer dans le dossier d'envoi.",
        ["DownloadStep2"] = "2. Scannez le code QR avec l'appareil photo du téléphone.",
        ["DownloadStep3"] = "3. Touchez les fichiers sur la page web pour les télécharger directement sur votre téléphone !",
        ["InstructionsNote"] = "🔒 Les fichiers sont transférés rapidement et en toute sécurité via le Wi-Fi local sans passer par Internet.",
        ["NetworkTroubleHint"] = "* En cas de problème de connexion, essayez 'Changer d'IP' ou vérifiez si l'isolation AP est désactivée sur votre routeur.",
        ["SupportedFormatsLabel"] = "Formats pris en charge : Photos (JPEG, PNG, HEIC...), PDF, JSON, CSV, GPX, ZIP et tous les autres fichiers",
        ["NoNetworkError"] = "Aucune connexion réseau active (Wi-Fi ou LAN) trouvée. Veuillez vérifier la connexion de votre PC.",
        ["ToolTipAbout"] = "ℹ️ À propos de l'application",
        ["VersionLabel"] = "Version",
        ["AboutAppDescription"] = "Outil de transfert direct bidirectionnel entre téléphone et PC via code QR.\nTransfert rapide et sécurisé au sein du réseau local.",
        ["HomepageLabel"] = "Site Web",
        ["CloseButton"] = "Fermer"
    };

    private static readonly Dictionary<string, string> PortugueseDictionary = new()
    {
        ["AppTitle"] = "Transferência Fácil de Arquivos",
        ["AppSubtitle"] = "Transferência direta local de alta velocidade entre celular e PC via QR Code",
        ["LanguageLabel"] = "Idioma:",
        ["TabUpload"] = "📤 Receber (Celular → PC)",
        ["TabDownload"] = "📥 Enviar (PC → Celular)",
        ["QrCodeTitle"] = "📱 Escaneie o código QR com a câmera do celular",
        ["QrCodeTitleUpload"] = "📱 Escaneie para enviar arquivos para o PC",
        ["QrCodeTitleDownload"] = "📱 Escaneie para baixar arquivos do PC",
        ["QrCodeHint"] = "Escaneie com a câmera para abrir a página de envio e transferir arquivos diretamente para o PC.",
        ["QrCodeHintUpload"] = "Escaneie com a câmera para abrir a página de envio de arquivos.",
        ["QrCodeHintDownload"] = "Escaneie com a câmera para abrir a página de download de arquivos.",
        ["TransferUrlLabel"] = "URL de transferência:",
        ["CopyUrlButton"] = "📋 Copiar URL",
        ["UrlCopiedToast"] = "URL copiada para a área de transferência",
        ["SwitchIpButton"] = "🔄 Alternar IP ({0}/{1})",
        ["SessionLabel"] = "Sessão:",
        ["RegenerateSessionButton"] = "🔄 Atualizar QR",
        ["SessionProtectedBadge"] = "🔒 Proteção 1:1",
        ["ServerStatusTitle"] = "Status da transferência:",
        ["StatusWaiting"] = "🟢 Aguardando (pronto para conexão)",
        ["StatusConnected"] = "🔵 Celular conectado (página web aberta)",
        ["StatusReceiving"] = "🔄 Recebendo arquivos...",
        ["StatusCompleted"] = "✅ Arquivo recebido e salvo com sucesso!",
        ["StatusDownloading"] = "⬇️ Enviando arquivo para o celular...",
        ["StatusDownloadCompleted"] = "✅ Download do arquivo concluído!",
        ["StatusError"] = "⚠️ Ocorreu um erro",
        ["TargetFolderTitle"] = "📁 Pasta de destino (Arquivos recebidos)",
        ["DownloadFolderTitle"] = "📁 Pasta de envio (Compartilhada com o celular)",
        ["ChangeFolderButton"] = "Alterar pasta",
        ["OpenFolderButton"] = "Abrir pasta",
        ["FolderNotFound"] = "Pasta não encontrada.",
        ["ReceivedFilesTitle"] = "📋 Arquivos recebidos",
        ["NoReceivedFiles"] = "Nenhum arquivo recebido ainda. Envie arquivos do seu celular.",
        ["SharedFilesTitle"] = "📂 Arquivos compartilhados (PC)",
        ["NoSharedFiles"] = "Nenhum arquivo na pasta de envio. Clique em '+ Adicionar arquivos'.",
        ["AddFilesButton"] = "＋ Adicionar arquivos",
        ["RefreshListButton"] = "🔄 Atualizar",
        ["ClearListButton"] = "Limpar histórico",
        ["OpenFileButton"] = "Abrir",
        ["DeleteButton"] = "Excluir",
        ["SavedBadge"] = "✓ Salvo",
        ["SelectFilesToShareTitle"] = "Selecione os arquivos para compartilhar com o celular",
        ["InstructionsTitle"] = "💡 Como receber (Celular → PC)",
        ["InstructionStep1"] = "1. Conecte o PC e o celular à mesma rede Wi-Fi.",
        ["InstructionStep2"] = "2. Escaneie o código QR à esquerda com a câmera do celular.",
        ["InstructionStep3"] = "3. Selecione fotos, PDFs, CSV, GPX, ZIP ou qualquer arquivo no celular para transferir imediatamente.",
        ["DownloadInstructionsTitle"] = "💡 Como enviar (PC → Celular)",
        ["DownloadStep1"] = "1. Adicione os arquivos que deseja enviar à pasta de envio.",
        ["DownloadStep2"] = "2. Escaneie o código QR com a câmera do celular.",
        ["DownloadStep3"] = "3. Toque nos arquivos na página web aberta para baixá-los diretamente no celular!",
        ["InstructionsNote"] = "🔒 Os arquivos são transferidos de forma direta e segura pelo Wi-Fi local sem passar pela Internet.",
        ["NetworkTroubleHint"] = "* Se não conseguir conectar, tente 'Alternar IP' ou verifique se o 'Isolamento de AP' está desativado no roteador.",
        ["SupportedFormatsLabel"] = "Formatos suportados: Fotos (JPEG, PNG, HEIC etc.), PDF, JSON, CSV, GPX, ZIP e todos os outros arquivos",
        ["NoNetworkError"] = "Nenhuma conexão de rede ativa (Wi-Fi ou LAN) encontrada. Verifique a conexão do PC.",
        ["ToolTipAbout"] = "ℹ️ Sobre o aplicativo",
        ["VersionLabel"] = "Versão",
        ["AboutAppDescription"] = "Ferramenta de transferência direta bidirecional entre celular e PC via QR Code.\nTransferência rápida e segura na rede local.",
        ["HomepageLabel"] = "Site oficial",
        ["CloseButton"] = "Fechar"
    };

    private static readonly Dictionary<string, string> ItalianDictionary = new()
    {
        ["AppTitle"] = "Trasferimento File Facile",
        ["AppSubtitle"] = "Trasferimento diretto locale ad alta velocità tra smartphone e PC tramite codice QR",
        ["LanguageLabel"] = "Lingua:",
        ["TabUpload"] = "📤 Ricevi (Smartphone → PC)",
        ["TabDownload"] = "📥 Invia (PC → Smartphone)",
        ["QrCodeTitle"] = "📱 Scansiona il codice QR con la fotocamera dello smartphone",
        ["QrCodeTitleUpload"] = "📱 Scansiona per inviare file al PC",
        ["QrCodeTitleDownload"] = "📱 Scansiona per scaricare file dal PC",
        ["QrCodeHint"] = "Scansiona con la fotocamera per aprire la pagina di invio e trasferire direttamente sul tuo PC.",
        ["QrCodeHintUpload"] = "Scansiona con la fotocamera per aprire la pagina di invio file.",
        ["QrCodeHintDownload"] = "Scansiona con la fotocamera per aprire la pagina di download file.",
        ["TransferUrlLabel"] = "URL di trasferimento:",
        ["CopyUrlButton"] = "📋 Copia URL",
        ["UrlCopiedToast"] = "URL copiato negli appunti",
        ["SwitchIpButton"] = "🔄 Cambia IP ({0}/{1})",
        ["SessionLabel"] = "Sessione:",
        ["RegenerateSessionButton"] = "🔄 Aggiorna QR",
        ["SessionProtectedBadge"] = "🔒 Protezione 1:1",
        ["ServerStatusTitle"] = "Stato del trasferimento:",
        ["StatusWaiting"] = "🟢 In attesa (pronto per la connessione)",
        ["StatusConnected"] = "🔵 Smartphone connesso (pagina web aperta)",
        ["StatusReceiving"] = "🔄 Ricezione file in corso...",
        ["StatusCompleted"] = "✅ File ricevuto e salvato con successo!",
        ["StatusDownloading"] = "⬇️ Invio file allo smartphone...",
        ["StatusDownloadCompleted"] = "✅ Download del file completato!",
        ["StatusError"] = "⚠️ Si è verificato un errore",
        ["TargetFolderTitle"] = "📁 Cartella di destinazione (File ricevuti)",
        ["DownloadFolderTitle"] = "📁 Cartella di invio (Condivisa con lo smartphone)",
        ["ChangeFolderButton"] = "Cambia cartella",
        ["OpenFolderButton"] = "Apri cartella",
        ["FolderNotFound"] = "Cartella non trovata.",
        ["ReceivedFilesTitle"] = "📋 File ricevuti",
        ["NoReceivedFiles"] = "Nessun file ricevuto. Invia file dal tuo smartphone.",
        ["SharedFilesTitle"] = "📂 File condivisi (PC)",
        ["NoSharedFiles"] = "Nessun file nella cartella di invio. Fai clic su '+ Aggiungi file'.",
        ["AddFilesButton"] = "＋ Aggiungi file",
        ["RefreshListButton"] = "🔄 Aggiorna",
        ["ClearListButton"] = "Cancella cronologia",
        ["OpenFileButton"] = "Apri",
        ["DeleteButton"] = "Elimina",
        ["SavedBadge"] = "✓ Salvato",
        ["SelectFilesToShareTitle"] = "Seleziona i file da condividere con lo smartphone",
        ["InstructionsTitle"] = "💡 Come ricevere (Smartphone → PC)",
        ["InstructionStep1"] = "1. Connetti il PC e lo smartphone alla stessa rete Wi-Fi.",
        ["InstructionStep2"] = "2. Scansiona il codice QR a sinistra con la fotocamera dello smartphone.",
        ["InstructionStep3"] = "3. Seleziona foto, PDF, CSV, GPX, ZIP o qualsiasi file per trasferirli immediatamente.",
        ["DownloadInstructionsTitle"] = "💡 Come inviare (PC → Smartphone)",
        ["DownloadStep1"] = "1. Aggiungi i file da inviare nella cartella di invio.",
        ["DownloadStep2"] = "2. Scansiona il codice QR a sinistra con la fotocamera dello smartphone.",
        ["DownloadStep3"] = "3. Tocca i file sulla pagina web aperta per scaricarli direttamente sullo smartphone!",
        ["InstructionsNote"] = "🔒 I file vengono trasferiti in modo sicuro e rapido tramite la rete Wi-Fi locale senza passare da Internet.",
        ["NetworkTroubleHint"] = "* In caso di problemi di connessione, prova 'Cambia IP' o verifica che l'isolamento AP sia disattivato sul router.",
        ["SupportedFormatsLabel"] = "Formati supportati: Foto (JPEG, PNG, HEIC...), PDF, JSON, CSV, GPX, ZIP e tutti gli altri file",
        ["NoNetworkError"] = "Nessuna connessione di rete attiva (Wi-Fi o LAN) trovata. Controlla la connessione del PC.",
        ["ToolTipAbout"] = "ℹ️ Informazioni sull'app",
        ["VersionLabel"] = "Versione",
        ["AboutAppDescription"] = "Strumento per trasferire file bidirezionalmente tra smartphone e PC tramite codice QR.\nTrasferimento rapido e sicuro all'interno della rete locale.",
        ["HomepageLabel"] = "Sito web",
        ["CloseButton"] = "Chiudi"
    };

    #endregion
}
