using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileTransfer.Models;
using EasyFileTransfer.Services;

namespace EasyFileTransfer.Linux.ViewModels;

/// <summary>
/// Linux（Avalonia UI）版のメイン画面を動かす ViewModel クラスです。
/// CommunityToolkit.Mvvm を使って、画面とのデータバインディングやボタンのコマンド処理をまとめています。
/// </summary>
public partial class MainViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// ステータスカードに表示するサーバーの動作状態です。
    /// </summary>
    private enum ServerUiStatus
    {
        Waiting,
        Connected,
        Receiving,
        Completed,
        Downloading,
        DownloadCompleted,
        Error
    }

    private readonly FileTransferServer _server = new();
    private int _currentIpIndex = 0;
    private TransferMode _currentMode = TransferMode.Upload;
    private ServerUiStatus _currentStatus = ServerUiStatus.Waiting;
    private string _statusParam = string.Empty;

    // View層とのコールバック連携
    public Func<Task<string?>>? PickFolderHandler { get; set; }
    public Func<Task<string[]>>? PickFilesHandler { get; set; }
    public Action<string>? SetClipboardTextHandler { get; set; }

    [ObservableProperty]
    private int _selectedLanguageIndex = 0;

    [ObservableProperty]
    private bool _isUploadMode = true;

    [ObservableProperty]
    private bool _isDownloadMode = false;

    [ObservableProperty]
    private string _transferUrl = string.Empty;

    [ObservableProperty]
    private Bitmap? _qrBitmap;

    [ObservableProperty]
    private string _sessionToken = "ID: ----";

    [ObservableProperty]
    private string _switchIpButtonText = "🌐 IP切替";

    [ObservableProperty]
    private bool _canSwitchIp = false;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private IBrush _statusColor = new SolidColorBrush(Color.Parse("#10B981"));

    [ObservableProperty]
    private bool _isStatusBusy = false;

    [ObservableProperty]
    private string _copyButtonText = "📋 URLコピー";

    [ObservableProperty]
    private string _uploadFolderPath = string.Empty;

    [ObservableProperty]
    private string _downloadFolderPath = string.Empty;

    [ObservableProperty]
    private bool _hasReceivedFiles = false;

    [ObservableProperty]
    private bool _hasSharedFiles = false;

    [ObservableProperty]
    private bool _isAboutModalVisible = false;

    [ObservableProperty]
    private string _appVersionText = "Version 1.1.0";

    public ObservableCollection<ReceivedFileInfo> ReceivedFiles { get; } = [];
    public ObservableCollection<SharedFileInfo> SharedFiles { get; } = [];

    // ローカライズ用アクセサ
    public LocalizationService Loc => LocalizationService.Instance;

    public string AppTitle => Loc.Get("AppTitle");
    public string AppSubtitle => Loc.Get("AppSubtitle");
    public string LanguageLabel => $"🌐 {Loc.Get("LanguageLabel")}";
    public string TabUploadText => Loc.Get("TabUpload");
    public string TabDownloadText => Loc.Get("TabDownload");
    public string TransferUrlLabel => Loc.Get("TransferUrlLabel");
    public string SessionProtectedBadge => Loc.Get("SessionProtectedBadge");
    public string RegenerateSessionButton => Loc.Get("RegenerateSessionButton");
    public string TargetFolderTitle => Loc.Get("TargetFolderTitle");
    public string DownloadFolderTitle => Loc.Get("DownloadFolderTitle");
    public string ChangeFolderButton => Loc.Get("ChangeFolderButton");
    public string OpenFolderButton => Loc.Get("OpenFolderButton");
    public string AddFilesButton => Loc.Get("AddFilesButton");
    public string InstructionsTitle => Loc.Get("InstructionsTitle");
    public string DownloadInstructionsTitle => Loc.Get("DownloadInstructionsTitle");
    public string InstructionStep1 => Loc.Get("InstructionStep1");
    public string InstructionStep2 => Loc.Get("InstructionStep2");
    public string InstructionStep3 => Loc.Get("InstructionStep3");
    public string DownloadStep1 => Loc.Get("DownloadStep1");
    public string DownloadStep2 => Loc.Get("DownloadStep2");
    public string DownloadStep3 => Loc.Get("DownloadStep3");
    public string InstructionsNote => Loc.Get("InstructionsNote");
    public string NetworkTroubleHint => Loc.Get("NetworkTroubleHint");
    public string ReceivedFilesTitle => Loc.Get("ReceivedFilesTitle");
    public string SharedFilesTitle => Loc.Get("SharedFilesTitle");
    public string NoReceivedFiles => Loc.Get("NoReceivedFiles");
    public string NoSharedFiles => Loc.Get("NoSharedFiles");
    public string RefreshListButton => Loc.Get("RefreshListButton");
    public string ClearListButton => Loc.Get("ClearListButton");
    public string SavedBadge => Loc.Get("SavedBadge");
    public string OpenFileButton => Loc.Get("OpenFileButton");
    public string AboutAppDescription => Loc.Get("AboutAppDescription");
    public string HomepageLabel => Loc.Get("HomepageLabel");
    public string CloseButton => Loc.Get("CloseButton");
    public string ToolTipAbout => Loc.Get("ToolTipAbout");

    public string QrCodeTitle => _currentMode == TransferMode.Upload
        ? Loc.Get("QrCodeTitleUpload")
        : Loc.Get("QrCodeTitleDownload");

    public string QrCodeHint => _currentMode == TransferMode.Upload
        ? Loc.Get("QrCodeHintUpload")
        : Loc.Get("QrCodeHintDownload");

    /// <summary>
    /// コンストラクタ：初期言語の設定、サーバーイベントの購読、そしてサーバー起動までを順番に実行します。
    /// </summary>
    public MainViewModel()
    {
        // バージョン取得
        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.0";
        AppVersionText = $"{Loc.Get("VersionLabel")}: {ver}";

        // 言語の初期値設定
        _selectedLanguageIndex = Loc.CurrentLanguage switch
        {
            AppLanguage.Japanese => 0,
            AppLanguage.English => 1,
            AppLanguage.Chinese => 2,
            AppLanguage.Korean => 3,
            AppLanguage.TraditionalChinese => 4,
            AppLanguage.Vietnamese => 5,
            AppLanguage.Spanish => 6,
            AppLanguage.German => 7,
            AppLanguage.French => 8,
            AppLanguage.Portuguese => 9,
            AppLanguage.Italian => 10,
            _ => 0
        };

        // 言語変更時の全プロパティ更新
        Loc.PropertyChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(string.Empty);
                CopyButtonText = Loc.Get("CopyUrlButton");
                AppVersionText = $"{Loc.Get("VersionLabel")}: {ver}";
                UpdateSwitchIpButton();
                UpdateStatusDisplay();
            });
        };

        CopyButtonText = Loc.Get("CopyUrlButton");

        // サーバーイベント購読
        _server.ClientConnected += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _currentStatus = ServerUiStatus.Connected;
                _statusParam = string.Empty;
                UpdateStatusDisplay();
            });
        };

        _server.UploadStarted += fileName =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _currentStatus = ServerUiStatus.Receiving;
                _statusParam = fileName;
                UpdateStatusDisplay();
            });
        };

        _server.FileReceived += fileInfo =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _currentStatus = ServerUiStatus.Completed;
                _statusParam = fileInfo.FileName;
                UpdateStatusDisplay();

                ReceivedFiles.Insert(0, fileInfo);
                HasReceivedFiles = ReceivedFiles.Count > 0;
            });
        };

        _server.FileDownloaded += fileName =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _currentStatus = ServerUiStatus.DownloadCompleted;
                _statusParam = fileName;
                UpdateStatusDisplay();
            });
        };

        _server.ErrorOccurred += err =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _currentStatus = ServerUiStatus.Error;
                _statusParam = err;
                UpdateStatusDisplay();
            });
        };

        UpdateFolderPaths();
        StartServerAndShowQr();
        LoadSharedFiles();
    }

    /// <summary>
    /// 言語選択のComboBoxが切り替わったときに自動実行される処理です。
    /// </summary>
    partial void OnSelectedLanguageIndexChanged(int value)
    {
        var targetLang = value switch
        {
            0 => AppLanguage.Japanese,
            1 => AppLanguage.English,
            2 => AppLanguage.Chinese,
            3 => AppLanguage.Korean,
            4 => AppLanguage.TraditionalChinese,
            5 => AppLanguage.Vietnamese,
            6 => AppLanguage.Spanish,
            7 => AppLanguage.German,
            8 => AppLanguage.French,
            9 => AppLanguage.Portuguese,
            10 => AppLanguage.Italian,
            _ => AppLanguage.Japanese
        };

        if (Loc.CurrentLanguage != targetLang)
        {
            Loc.CurrentLanguage = targetLang;
        }
    }

    #region Tab Management

    /// <summary>
    /// 「受信（スマホ → PC）」タブに切り替えるコマンドです。
    /// </summary>
    [RelayCommand]
    private void SwitchToUpload()
    {
        SwitchMode(TransferMode.Upload);
    }

    /// <summary>
    /// 「送信（PC → スマホ）」タブに切り替えるコマンドです。
    /// </summary>
    [RelayCommand]
    private void SwitchToDownload()
    {
        SwitchMode(TransferMode.Download);
    }

    /// <summary>
    /// 受信/送信モードを切り替えて、QRコードと表示内容を更新します。
    /// </summary>
    private void SwitchMode(TransferMode mode)
    {
        _currentMode = mode;
        IsUploadMode = mode == TransferMode.Upload;
        IsDownloadMode = mode == TransferMode.Download;

        if (mode == TransferMode.Download)
        {
            LoadSharedFiles();
        }

        UpdateQrCodeAndUrl();
        _currentStatus = ServerUiStatus.Waiting;
        _statusParam = string.Empty;
        UpdateStatusDisplay();

        OnPropertyChanged(nameof(QrCodeTitle));
        OnPropertyChanged(nameof(QrCodeHint));
    }

    #endregion

    #region Server & QR

    /// <summary>
    /// HTTPサーバーを起動して、最初のQRコードを生成・表示します。
    /// </summary>
    private void StartServerAndShowQr()
    {
        try
        {
            _server.Start(5050);
            _currentIpIndex = 0;
            UpdateQrCodeAndUrl();
            _currentStatus = ServerUiStatus.Waiting;
            _statusParam = string.Empty;
            UpdateStatusDisplay();
        }
        catch (Exception ex)
        {
            _currentStatus = ServerUiStatus.Error;
            _statusParam = ex.Message;
            UpdateStatusDisplay();
            CanSwitchIp = false;
        }
    }

    /// <summary>
    /// 現在のIPアドレスとセッションIDからURLを作り直し、Avalonia用のBitmapとしてQRコードを描画します。
    /// </summary>
    private void UpdateQrCodeAndUrl()
    {
        if (_server.IpCandidates.Count == 0) return;

        if (_currentIpIndex >= _server.IpCandidates.Count)
        {
            _currentIpIndex = 0;
        }

        var candidate = _server.IpCandidates[_currentIpIndex];
        string url = _server.GetTransferUrl(candidate.IpAddress, _currentMode);
        TransferUrl = url;
        SessionToken = $"ID: {_server.CurrentSession?.Token ?? "----"}";
        UpdateSwitchIpButton();

        try
        {
            byte[] bytes = QrCodeGenerator.GeneratePngBytes(url, pixelsPerModule: 8);
            using var ms = new MemoryStream(bytes);
            QrBitmap = new Bitmap(ms);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] QR Code Generate failed: {ex.Message}");
        }
    }

    /// <summary>
    /// IP候補が複数見つかった場合に「IP切替」ボタンの表示・テキストを更新します。
    /// </summary>
    private void UpdateSwitchIpButton()
    {
        if (_server.IpCandidates.Count > 1)
        {
            CanSwitchIp = true;
            SwitchIpButtonText = Loc.Format("SwitchIpButton", _currentIpIndex + 1, _server.IpCandidates.Count);
        }
        else
        {
            CanSwitchIp = false;
        }
    }

    /// <summary>
    /// 別のローカルIPアドレス候補へ切り替えます。
    /// </summary>
    [RelayCommand]
    private void SwitchIp()
    {
        if (_server.IpCandidates.Count <= 1) return;
        _currentIpIndex = (_currentIpIndex + 1) % _server.IpCandidates.Count;
        UpdateQrCodeAndUrl();
    }

    /// <summary>
    /// 新しい使い捨てセッションを発行してQRコードを作り直します。
    /// </summary>
    [RelayCommand]
    private void RegenerateSession()
    {
        _server.CreateNewSession(_currentMode);
        UpdateQrCodeAndUrl();
    }

    /// <summary>
    /// 転送用URLをクリップボードにコピーするコマンドです。
    /// </summary>
    [RelayCommand]
    private async Task CopyUrl()
    {
        if (string.IsNullOrWhiteSpace(TransferUrl)) return;

        SetClipboardTextHandler?.Invoke(TransferUrl);

        CopyButtonText = "✅ " + Loc.Get("UrlCopiedToast");
        await Task.Delay(1500);
        CopyButtonText = Loc.Get("CopyUrlButton");
    }

    /// <summary>
    /// 現在のステータスに合わせて画面の文言と色・プログレスバー表示を更新します。
    /// </summary>
    private void UpdateStatusDisplay()
    {
        switch (_currentStatus)
        {
            case ServerUiStatus.Waiting:
                IsStatusBusy = false;
                StatusText = Loc.Get("StatusWaiting");
                StatusColor = new SolidColorBrush(Color.Parse("#10B981"));
                break;

            case ServerUiStatus.Connected:
                IsStatusBusy = false;
                StatusText = Loc.Get("StatusConnected");
                StatusColor = new SolidColorBrush(Color.Parse("#3B82F6"));
                break;

            case ServerUiStatus.Receiving:
                IsStatusBusy = true;
                StatusText = string.IsNullOrEmpty(_statusParam)
                    ? Loc.Get("StatusReceiving")
                    : $"{Loc.Get("StatusReceiving")} ({_statusParam})";
                StatusColor = new SolidColorBrush(Color.Parse("#6366F1"));
                break;

            case ServerUiStatus.Completed:
                IsStatusBusy = false;
                StatusText = string.IsNullOrEmpty(_statusParam)
                    ? Loc.Get("StatusCompleted")
                    : $"{Loc.Get("StatusCompleted")} ({_statusParam})";
                StatusColor = new SolidColorBrush(Color.Parse("#10B981"));
                break;

            case ServerUiStatus.Downloading:
                IsStatusBusy = true;
                StatusText = string.IsNullOrEmpty(_statusParam)
                    ? Loc.Get("StatusDownloading")
                    : $"{Loc.Get("StatusDownloading")} ({_statusParam})";
                StatusColor = new SolidColorBrush(Color.Parse("#6366F1"));
                break;

            case ServerUiStatus.DownloadCompleted:
                IsStatusBusy = false;
                StatusText = string.IsNullOrEmpty(_statusParam)
                    ? Loc.Get("StatusDownloadCompleted")
                    : $"{Loc.Get("StatusDownloadCompleted")} ({_statusParam})";
                StatusColor = new SolidColorBrush(Color.Parse("#10B981"));
                break;

            case ServerUiStatus.Error:
                IsStatusBusy = false;
                StatusText = $"{Loc.Get("StatusError")}: {_statusParam}";
                StatusColor = new SolidColorBrush(Color.Parse("#EF4444"));
                break;
        }
    }

    #endregion

    #region Folder Management

    /// <summary>
    /// 画面に表示する受信先・共有元フォルダーのパスを最新にします。
    /// </summary>
    public void UpdateFolderPaths()
    {
        UploadFolderPath = StorageSettings.GetUploadFolder();
        DownloadFolderPath = StorageSettings.GetDownloadFolder();
    }

    /// <summary>
    /// 受信ファイルの保存先フォルダーを変更するコマンドです。
    /// </summary>
    [RelayCommand]
    private async Task ChangeUploadFolder()
    {
        if (PickFolderHandler != null)
        {
            var picked = await PickFolderHandler();
            if (!string.IsNullOrWhiteSpace(picked))
            {
                StorageSettings.SetUploadFolder(picked);
                UpdateFolderPaths();
            }
        }
    }

    /// <summary>
    /// 受信ファイルの保存先フォルダーをファイルマネージャーで開きます。
    /// </summary>
    [RelayCommand]
    private void OpenUploadFolder()
    {
        StorageSettings.OpenInExplorer(StorageSettings.GetUploadFolder());
    }

    /// <summary>
    /// スマホへ共有する側のフォルダーを変更するコマンドです。
    /// </summary>
    [RelayCommand]
    private async Task ChangeDownloadFolder()
    {
        if (PickFolderHandler != null)
        {
            var picked = await PickFolderHandler();
            if (!string.IsNullOrWhiteSpace(picked))
            {
                StorageSettings.SetDownloadFolder(picked);
                UpdateFolderPaths();
                LoadSharedFiles();
            }
        }
    }

    /// <summary>
    /// 共有フォルダーをファイルマネージャーで開きます。
    /// </summary>
    [RelayCommand]
    private void OpenDownloadFolder()
    {
        StorageSettings.OpenInExplorer(StorageSettings.GetDownloadFolder());
    }

    /// <summary>
    /// ファイル選択ダイアログから選んだファイルを共有フォルダーへコピーします。
    /// </summary>
    [RelayCommand]
    private async Task AddFilesToShare()
    {
        if (PickFilesHandler != null)
        {
            var files = await PickFilesHandler();
            if (files != null && files.Length > 0)
            {
                string targetFolder = StorageSettings.GetDownloadFolder();
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                foreach (var file in files)
                {
                    try
                    {
                        var fileName = Path.GetFileName(file);
                        var dest = Path.Combine(targetFolder, fileName);
                        File.Copy(file, dest, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MainViewModel] AddFilesToShare error: {ex.Message}");
                    }
                }

                LoadSharedFiles();
            }
        }
    }

    #endregion

    #region File List Management

    /// <summary>
    /// 一覧の「開く」ボタンから該当ファイルを開きます。
    /// </summary>
    [RelayCommand]
    private void OpenFile(string fullPath)
    {
        StorageSettings.OpenFile(fullPath);
    }

    /// <summary>
    /// 受信履歴のうち、実体ファイルが消えているものを除外して更新します。
    /// </summary>
    [RelayCommand]
    private void RefreshReceivedFiles()
    {
        for (int i = ReceivedFiles.Count - 1; i >= 0; i--)
        {
            if (!File.Exists(ReceivedFiles[i].FullPath))
            {
                ReceivedFiles.RemoveAt(i);
            }
        }
        HasReceivedFiles = ReceivedFiles.Count > 0;
    }

    /// <summary>
    /// 画面上の受信リストをクリアします（PCに保存されたファイルは残ります）。
    /// </summary>
    [RelayCommand]
    private void ClearReceivedFiles()
    {
        ReceivedFiles.Clear();
        HasReceivedFiles = false;
    }

    /// <summary>
    /// 共有ファイル一覧を再読み込みします。
    /// </summary>
    [RelayCommand]
    private void RefreshSharedFiles()
    {
        LoadSharedFiles();
    }

    /// <summary>
    /// 共有フォルダーにある不要なファイルを削除します。
    /// </summary>
    [RelayCommand]
    private void DeleteSharedFile(string fullPath)
    {
        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                LoadSharedFiles();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] DeleteSharedFile error: {ex.Message}");
        }
    }

    /// <summary>
    /// 共有フォルダー内のファイルを新しい順に読み込んで SharedFiles コレクションに入れます。
    /// </summary>
    private void LoadSharedFiles()
    {
        SharedFiles.Clear();
        string folder = StorageSettings.GetDownloadFolder();

        if (Directory.Exists(folder))
        {
            try
            {
                var di = new DirectoryInfo(folder);
                foreach (var fi in di.GetFiles().OrderByDescending(f => f.LastWriteTime))
                {
                    SharedFiles.Add(new SharedFileInfo
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
                Debug.WriteLine($"[MainViewModel] LoadSharedFiles error: {ex.Message}");
            }
        }

        HasSharedFiles = SharedFiles.Count > 0;
    }

    #endregion

    #region About Modal & Links

    /// <summary>
    /// アプリ情報モーダルを開きます。
    /// </summary>
    [RelayCommand]
    private void OpenAbout()
    {
        IsAboutModalVisible = true;
    }

    /// <summary>
    /// アプリ情報モーダルを閉じます。
    /// </summary>
    [RelayCommand]
    private void CloseAbout()
    {
        IsAboutModalVisible = false;
    }

    /// <summary>
    /// ホームページURLをブラウザで開きます。
    /// </summary>
    [RelayCommand]
    private void OpenHomepage()
    {
        OpenUrl("https://sites.google.com/view/kuma-applications/%E7%B0%A1%E5%8D%98%E3%83%95%E3%82%A1%E3%82%A4%E3%83%AB%E8%BB%A2%E9%80%81");
    }

    /// <summary>
    /// Instagramリンクをブラウザで開きます。
    /// </summary>
    [RelayCommand]
    private void OpenInstagram()
    {
        OpenUrl("https://www.instagram.com/zefa_25_zefa/");
    }

    /// <summary>
    /// OSに合わせた方法で外部URLをブラウザで開くヘルパーメソッドです。
    /// </summary>
    private static void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start(new ProcessStartInfo { FileName = "xdg-open", Arguments = $"\"{url}\"", UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] OpenUrl error: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// ViewModel が破棄されるときにHTTPサーバーを停止・解放します。
    /// </summary>
    public void Dispose()
    {
        _server.Dispose();
    }
}
