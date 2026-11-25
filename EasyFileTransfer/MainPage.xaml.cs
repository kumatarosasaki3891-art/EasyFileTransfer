using System.Collections.ObjectModel;
using System.Diagnostics;
using EasyFileTransfer.Models;
using EasyFileTransfer.Services;
using Microsoft.Maui.Controls.Shapes;

namespace EasyFileTransfer;

/// <summary>
/// メイン画面のコードビハインドです。
/// QRコードの生成、サーバーの起動、受信・送信ファイル一覧の表示などをまとめて動かしています。
/// </summary>
public partial class MainPage : ContentPage
{
    /// <summary>
    /// 画面下（左側カード）に表示するサーバーの状態パターンです
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

    private readonly ObservableCollection<ReceivedFileInfo> _receivedFiles = [];
    private readonly ObservableCollection<SharedFileInfo> _sharedFiles = [];

    private ServerUiStatus _currentStatus = ServerUiStatus.Waiting;
    private string _statusParam = string.Empty;

    /// <summary>
    /// コンストラクタ：UIの初期化と、言語切替やサーバーからの通知を受け取る設定をします。
    /// </summary>
    public MainPage()
    {
        InitializeComponent();

        // 言語変更通知の登録
        LocalizationService.Instance.PropertyChanged += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyTranslations();
                UpdateQrCodeAndUrl();
            });
        };

        // サーバーイベントの購読
        _server.ClientConnected += OnServerClientConnected;
        _server.UploadStarted += OnServerUploadStarted;
        _server.FileReceived += OnServerFileReceived;
        _server.FileDownloaded += OnServerFileDownloaded;
        _server.ErrorOccurred += OnServerErrorOccurred;
    }

    /// <summary>
    /// 画面が表示されるときに呼ばれます。言語やフォルダーをセットしてHTTPサーバーを起動します。
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        InitLanguagePicker();
        ApplyTranslations();
        UpdateFolderPaths();
        StartServerAndShowQr();
        LoadSharedFiles();
    }

    /// <summary>
    /// 画面が閉じられるときにサーバーを止めます。
    /// </summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _server.Stop();
    }

    /// <summary>
    /// 今の言語設定に合わせて、右上の言語ドロップダウンの選択位置を合わせます。
    /// </summary>
    private void InitLanguagePicker()
    {
        LanguagePicker.SelectedIndex = LocalizationService.Instance.CurrentLanguage switch
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
    }

    /// <summary>
    /// 言語ドロップダウンが変更されたときに、アプリ全体の言語を切り替えます。
    /// </summary>
    private void OnLanguagePickerChanged(object? sender, EventArgs e)
    {
        var selected = LanguagePicker.SelectedIndex switch
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

        if (LocalizationService.Instance.CurrentLanguage != selected)
        {
            LocalizationService.Instance.CurrentLanguage = selected;
        }
    }

    #region Tab Management (Upload / Download)

    /// <summary>
    /// 「受信（スマホ → PC）」タブが押されたときの処理です。
    /// </summary>
    private void OnTabUploadClicked(object? sender, EventArgs e)
    {
        SwitchTransferMode(TransferMode.Upload);
    }

    /// <summary>
    /// 「送信（PC → スマホ）」タブが押されたときの処理です。
    /// </summary>
    private void OnTabDownloadClicked(object? sender, EventArgs e)
    {
        SwitchTransferMode(TransferMode.Download);
    }

    /// <summary>
    /// 受信モードと送信モードを切り替えて、QRコードや表示パネルを更新します。
    /// </summary>
    private void SwitchTransferMode(TransferMode mode)
    {
        _currentMode = mode;
        UpdateTabButtonsAppearance();

        if (mode == TransferMode.Upload)
        {
            PanelUpload.IsVisible = true;
            PanelDownload.IsVisible = false;
        }
        else
        {
            PanelUpload.IsVisible = false;
            PanelDownload.IsVisible = true;
            LoadSharedFiles();
        }

        UpdateQrCodeAndUrl();
        UpdateTitlesAndHints();
        _currentStatus = ServerUiStatus.Waiting;
        _statusParam = string.Empty;
        UpdateStatusDisplay();
    }

    /// <summary>
    /// 選択されているタブの色を目立たせるようにボタンの見た目を更新します。
    /// </summary>
    private void UpdateTabButtonsAppearance()
    {
        var primaryColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#6366F1")
            : Color.FromArgb("#4F46E5");

        var mutedColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#94A3B8")
            : Color.FromArgb("#64748B");

        if (_currentMode == TransferMode.Upload)
        {
            BtnTabUpload.BackgroundColor = primaryColor;
            BtnTabUpload.TextColor = Colors.White;

            BtnTabDownload.BackgroundColor = Colors.Transparent;
            BtnTabDownload.TextColor = mutedColor;
        }
        else
        {
            BtnTabDownload.BackgroundColor = primaryColor;
            BtnTabDownload.TextColor = Colors.White;

            BtnTabUpload.BackgroundColor = Colors.Transparent;
            BtnTabUpload.TextColor = mutedColor;
        }
    }

    #endregion

    #region Translations & UI Updates

    /// <summary>
    /// 画面上のあらゆるラベル・ボタンの文言を現在の言語で書き換えます。
    /// </summary>
    private void ApplyTranslations()
    {
        var loc = LocalizationService.Instance;

        LblAppTitle.Text = loc.Get("AppTitle");
        LblAppSubtitle.Text = loc.Get("AppSubtitle");
        LblLanguageTitle.Text = $"🌐 {loc.Get("LanguageLabel")}";

        BtnTabUpload.Text = loc.Get("TabUpload");
        BtnTabDownload.Text = loc.Get("TabDownload");

        LblUrlTitle.Text = loc.Get("TransferUrlLabel");
        BtnCopyUrl.Text = loc.Get("CopyUrlButton");

        LblSessionProtectedBadge.Text = loc.Get("SessionProtectedBadge");
        BtnRegenerateSession.Text = loc.Get("RegenerateSessionButton");

        // アップロード用パネル
        LblTargetFolderTitle.Text = loc.Get("TargetFolderTitle");
        BtnChangeFolder.Text = loc.Get("ChangeFolderButton");
        BtnOpenFolder.Text = loc.Get("OpenFolderButton");
        LblInstructionsTitle.Text = loc.Get("InstructionsTitle");
        LblStep1.Text = loc.Get("InstructionStep1");
        LblStep2.Text = loc.Get("InstructionStep2");
        LblStep3.Text = loc.Get("InstructionStep3");
        LblSecurityNote.Text = loc.Get("InstructionsNote");
        LblNetworkTrouble.Text = loc.Get("NetworkTroubleHint");
        LblReceivedTitle.Text = loc.Get("ReceivedFilesTitle");
        LblEmptyList.Text = loc.Get("NoReceivedFiles");
        BtnRefreshReceived.Text = loc.Get("RefreshListButton");
        BtnClearList.Text = loc.Get("ClearListButton");

        // ダウンロード用パネル
        LblDownloadFolderTitle.Text = loc.Get("DownloadFolderTitle");
        BtnAddFilesToShare.Text = loc.Get("AddFilesButton");
        BtnChangeDownloadFolder.Text = loc.Get("ChangeFolderButton");
        BtnOpenDownloadFolder.Text = loc.Get("OpenFolderButton");
        LblDownloadInstructionsTitle.Text = loc.Get("DownloadInstructionsTitle");
        LblDownloadStep1.Text = loc.Get("DownloadStep1");
        LblDownloadStep2.Text = loc.Get("DownloadStep2");
        LblDownloadStep3.Text = loc.Get("DownloadStep3");
        LblDownloadSecurityNote.Text = loc.Get("InstructionsNote");
        LblDownloadNetworkTrouble.Text = loc.Get("NetworkTroubleHint");
        LblSharedFilesTitle.Text = loc.Get("SharedFilesTitle");
        LblEmptySharedList.Text = loc.Get("NoSharedFiles");
        BtnRefreshSharedFiles.Text = loc.Get("RefreshListButton");

        if (BtnAbout != null) ToolTipProperties.SetText(BtnAbout, loc.Get("ToolTipAbout"));
        if (AboutTitleLabel != null) AboutTitleLabel.Text = loc.Get("AppTitle");
        if (AboutDescriptionLabel != null) AboutDescriptionLabel.Text = loc.Get("AboutAppDescription");
        if (AboutHomepageTitle != null) AboutHomepageTitle.Text = loc.Get("HomepageLabel");
        if (AboutCloseButton != null) AboutCloseButton.Text = loc.Get("CloseButton");

        UpdateAboutVersion();
        UpdateTitlesAndHints();
        UpdateSwitchIpButtonText();
        UpdateStatusDisplay();
        RebuildReceivedFilesList();
        RebuildSharedFilesList();
    }

    /// <summary>
    /// 受信モード・送信モードに合わせてQRコード上のタイトルとヒント文を差し替えます。
    /// </summary>
    private void UpdateTitlesAndHints()
    {
        var loc = LocalizationService.Instance;
        if (_currentMode == TransferMode.Upload)
        {
            LblQrCodeTitle.Text = loc.Get("QrCodeTitleUpload");
            LblQrHint.Text = loc.Get("QrCodeHintUpload");
        }
        else
        {
            LblQrCodeTitle.Text = loc.Get("QrCodeTitleDownload");
            LblQrHint.Text = loc.Get("QrCodeHintDownload");
        }
    }

    /// <summary>
    /// 現在の接続状況（待機中・受信中・完了など）をラベルとスピナーに反映します。
    /// </summary>
    private void UpdateStatusDisplay()
    {
        var loc = LocalizationService.Instance;

        switch (_currentStatus)
        {
            case ServerUiStatus.Waiting:
                StatusSpinner.IsVisible = false;
                StatusSpinner.IsRunning = false;
                LblStatus.Text = loc.Get("StatusWaiting");
                LblStatus.TextColor = Color.FromArgb("#10B981");
                break;

            case ServerUiStatus.Connected:
                StatusSpinner.IsVisible = false;
                StatusSpinner.IsRunning = false;
                LblStatus.Text = loc.Get("StatusConnected");
                LblStatus.TextColor = Color.FromArgb("#3B82F6");
                break;

            case ServerUiStatus.Receiving:
                StatusSpinner.IsVisible = true;
                StatusSpinner.IsRunning = true;
                LblStatus.Text = string.IsNullOrEmpty(_statusParam)
                    ? loc.Get("StatusReceiving")
                    : $"{loc.Get("StatusReceiving")} ({_statusParam})";
                LblStatus.TextColor = Color.FromArgb("#6366F1");
                break;

            case ServerUiStatus.Completed:
                StatusSpinner.IsVisible = false;
                StatusSpinner.IsRunning = false;
                LblStatus.Text = string.IsNullOrEmpty(_statusParam)
                    ? loc.Get("StatusCompleted")
                    : $"{loc.Get("StatusCompleted")} ({_statusParam})";
                LblStatus.TextColor = Color.FromArgb("#10B981");
                break;

            case ServerUiStatus.Downloading:
                StatusSpinner.IsVisible = true;
                StatusSpinner.IsRunning = true;
                LblStatus.Text = string.IsNullOrEmpty(_statusParam)
                    ? loc.Get("StatusDownloading")
                    : $"{loc.Get("StatusDownloading")} ({_statusParam})";
                LblStatus.TextColor = Color.FromArgb("#6366F1");
                break;

            case ServerUiStatus.DownloadCompleted:
                StatusSpinner.IsVisible = false;
                StatusSpinner.IsRunning = false;
                LblStatus.Text = string.IsNullOrEmpty(_statusParam)
                    ? loc.Get("StatusDownloadCompleted")
                    : $"{loc.Get("StatusDownloadCompleted")} ({_statusParam})";
                LblStatus.TextColor = Color.FromArgb("#10B981");
                break;

            case ServerUiStatus.Error:
                StatusSpinner.IsVisible = false;
                StatusSpinner.IsRunning = false;
                LblStatus.Text = $"{loc.Get("StatusError")}: {_statusParam}";
                LblStatus.TextColor = Colors.Red;
                break;
        }
    }

    #endregion

    #region Server & QR Code

    /// <summary>
    /// ローカルHTTPサーバーを立ち上げて、最初のQRコードを画面に表示します。
    /// </summary>
    private void StartServerAndShowQr()
    {
        try
        {
            _server.Start(5050);
            _currentIpIndex = 0;
            UpdateQrCodeAndUrl();
            UpdateSessionDisplay();
            _currentStatus = ServerUiStatus.Waiting;
            _statusParam = string.Empty;
            UpdateStatusDisplay();
        }
        catch (Exception ex)
        {
            _currentStatus = ServerUiStatus.Error;
            _statusParam = ex.Message;
            UpdateStatusDisplay();
            BtnSwitchIp.IsVisible = false;
        }
    }

    /// <summary>
    /// 現在選ばれているIPアドレスとモードからURLを作り直して、QRコード画像を再生成します。
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

        LblTransferUrl.Text = url;
        UpdateSwitchIpButtonText();
        UpdateSessionDisplay();

        try
        {
            QrCodeImage.Source = QrCodeGenerator.GenerateImageSource(url, pixelsPerModule: 8);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] QR Generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 現在の使い捨てセッションID（8文字）を画面に表示します。
    /// </summary>
    private void UpdateSessionDisplay()
    {
        var token = _server.CurrentSession?.Token ?? "----";
        LblSessionInfo.Text = $"ID: {token}";
    }

    /// <summary>
    /// 「QR更新」ボタンが押されたときに新しいセッションを発行してQRを作り直します。
    /// </summary>
    private void OnRegenerateSessionClicked(object? sender, EventArgs e)
    {
        _server.CreateNewSession(_currentMode);
        UpdateQrCodeAndUrl();
        UpdateSessionDisplay();
    }

    /// <summary>
    /// IP候補が複数見つかったときだけ「IP切替」ボタンを表示して件数を出します。
    /// </summary>
    private void UpdateSwitchIpButtonText()
    {
        if (_server.IpCandidates.Count > 1)
        {
            BtnSwitchIp.IsVisible = true;
            BtnSwitchIp.Text = LocalizationService.Instance.Format("SwitchIpButton", _currentIpIndex + 1, _server.IpCandidates.Count);
        }
        else
        {
            BtnSwitchIp.IsVisible = false;
        }
    }

    /// <summary>
    /// 「IP切替」ボタンが押されたら、次のネットワークIP候補へ切り替えます。
    /// 仮想NICのIPが最初に選ばれてしまったときに使います。
    /// </summary>
    private void OnSwitchIpClicked(object? sender, EventArgs e)
    {
        if (_server.IpCandidates.Count <= 1) return;

        _currentIpIndex = (_currentIpIndex + 1) % _server.IpCandidates.Count;
        UpdateQrCodeAndUrl();
    }

    /// <summary>
    /// 「URLコピー」ボタンで転送用URLをクリップボードにコピーします。
    /// </summary>
    private async void OnCopyUrlClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LblTransferUrl.Text)) return;

        await Clipboard.Default.SetTextAsync(LblTransferUrl.Text);
        var originalText = BtnCopyUrl.Text;
        BtnCopyUrl.Text = "✅ " + LocalizationService.Instance.Get("UrlCopiedToast");
        await Task.Delay(1500);
        BtnCopyUrl.Text = LocalizationService.Instance.Get("CopyUrlButton");
    }

    #endregion

    #region Folder Management

    /// <summary>
    /// 受信先フォルダーと共有フォルダーの現在のパスを画面に表示します。
    /// </summary>
    private void UpdateFolderPaths()
    {
        LblTargetFolderPath.Text = StorageSettings.GetUploadFolder();
        LblDownloadFolderPath.Text = StorageSettings.GetDownloadFolder();
    }

    /// <summary>
    /// 受信ファイルの保存先フォルダーを変更するボタンの処理です。
    /// </summary>
    private async void OnChangeFolderClicked(object? sender, EventArgs e)
    {
        var picked = await StorageSettings.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(picked))
        {
            StorageSettings.SetUploadFolder(picked);
            UpdateFolderPaths();
        }
    }

    /// <summary>
    /// 受信ファイルの保存先フォルダーをエクスプローラーで開きます。
    /// </summary>
    private void OnOpenFolderClicked(object? sender, EventArgs e)
    {
        StorageSettings.OpenInExplorer(StorageSettings.GetUploadFolder());
    }

    /// <summary>
    /// スマホへ共有する側のフォルダーを変更するボタンの処理です。
    /// </summary>
    private async void OnChangeDownloadFolderClicked(object? sender, EventArgs e)
    {
        var picked = await StorageSettings.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(picked))
        {
            StorageSettings.SetDownloadFolder(picked);
            UpdateFolderPaths();
            LoadSharedFiles();
        }
    }

    /// <summary>
    /// 共有フォルダーをエクスプローラーで開きます。
    /// </summary>
    private void OnOpenDownloadFolderClicked(object? sender, EventArgs e)
    {
        StorageSettings.OpenInExplorer(StorageSettings.GetDownloadFolder());
    }

    /// <summary>
    /// 「＋ファイル追加」ボタンから選んだファイルを共有フォルダーへコピーします。
    /// </summary>
    private async void OnAddFilesToShareClicked(object? sender, EventArgs e)
    {
        int copied = await StorageSettings.PickAndCopyFilesToFolderAsync(StorageSettings.GetDownloadFolder());
        if (copied > 0)
        {
            LoadSharedFiles();
        }
    }

    #endregion

    #region Server Event Handlers

    /// <summary>
    /// スマホがWebページを開いたときにサーバーから呼ばれるコールバックです。
    /// 別スレッドから来るので MainThread に戻してから画面を更新しています。
    /// </summary>
    private void OnServerClientConnected()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentStatus = ServerUiStatus.Connected;
            _statusParam = string.Empty;
            UpdateStatusDisplay();
        });
    }

    /// <summary>
    /// スマホからファイル受信が始まったときの処理です。
    /// </summary>
    private void OnServerUploadStarted(string fileName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentStatus = ServerUiStatus.Receiving;
            _statusParam = fileName;
            UpdateStatusDisplay();
        });
    }

    /// <summary>
    /// ファイルの受信・保存が終わったときに一覧の先頭へ追加します。
    /// </summary>
    private void OnServerFileReceived(ReceivedFileInfo fileInfo)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentStatus = ServerUiStatus.Completed;
            _statusParam = fileInfo.FileName;
            UpdateStatusDisplay();

            _receivedFiles.Insert(0, fileInfo);
            RebuildReceivedFilesList();
        });
    }

    /// <summary>
    /// スマホ側へのファイル送信（ダウンロード）が完了したときの処理です。
    /// </summary>
    private void OnServerFileDownloaded(string fileName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentStatus = ServerUiStatus.DownloadCompleted;
            _statusParam = fileName;
            UpdateStatusDisplay();
        });
    }

    /// <summary>
    /// サーバー側でエラーが起きたときにステータス欄をエラー表示にします。
    /// </summary>
    private void OnServerErrorOccurred(string error)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentStatus = ServerUiStatus.Error;
            _statusParam = error;
            UpdateStatusDisplay();
        });
    }

    #endregion

    #region Received Files Management (Upload Panel)

    /// <summary>
    /// 受信したファイル一覧のUIパーツをコードから組み立て直して表示します。
    /// </summary>
    private void RebuildReceivedFilesList()
    {
        ReceivedFilesStack.Children.Clear();
        if (_receivedFiles.Count == 0)
        {
            EmptyReceivedContainer.IsVisible = true;
            return;
        }

        EmptyReceivedContainer.IsVisible = false;
        var loc = LocalizationService.Instance;

        foreach (var fileInfo in _receivedFiles)
        {
            var rowBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 4)
            };
            rowBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E2E8F0"), Color.FromArgb("#334155"));
            rowBorder.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F8FAFC"), Color.FromArgb("#0F172A"));

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                ],
                ColumnSpacing = 10,
                VerticalOptions = LayoutOptions.Center
            };

            var iconLabel = new Label
            {
                Text = fileInfo.TypeIcon,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Center
            };

            var textStack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center
            };

            var nameLabel = new Label
            {
                Text = fileInfo.FileName,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.MiddleTruncation,
                VerticalOptions = LayoutOptions.Center
            };
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1E293B"), Color.FromArgb("#F8FAFC"));

            var subLabel = new Label
            {
                Text = $"{fileInfo.FormattedSize}  •  {fileInfo.FormattedTime}",
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            };
            subLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#64748B"), Color.FromArgb("#94A3B8"));

            textStack.Children.Add(nameLabel);
            textStack.Children.Add(subLabel);

            var successBadge = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(4) },
                BackgroundColor = Color.FromArgb("#ECFDF5"),
                Stroke = Color.FromArgb("#A7F3D0"),
                Padding = new Thickness(6, 2),
                VerticalOptions = LayoutOptions.Center
            };
            var successLabel = new Label
            {
                Text = loc.Get("SavedBadge"),
                FontSize = 10.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#059669")
            };
            successBadge.Content = successLabel;

            var openBtn = new Button
            {
                Text = loc.Get("OpenFileButton"),
                FontSize = 11.5,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 30,
                Padding = new Thickness(12, 0),
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#EEF2FF"),
                TextColor = Color.FromArgb("#4F46E5"),
                VerticalOptions = LayoutOptions.Center
            };
            openBtn.Clicked += (_, _) =>
            {
                try
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileInfo.FullPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainPage] Open file error: {ex.Message}");
                }
            };

            grid.Add(iconLabel, 0, 0);
            grid.Add(textStack, 1, 0);
            grid.Add(successBadge, 2, 0);
            grid.Add(openBtn, 3, 0);

            rowBorder.Content = grid;
            ReceivedFilesStack.Children.Add(rowBorder);
        }
    }

    /// <summary>
    /// 「更新」ボタン：PC上から消されたファイルがあれば一覧からも取り除きます。
    /// </summary>
    private void OnRefreshReceivedClicked(object? sender, EventArgs e)
    {
        // 存在しなくなったファイルをリストから除去して再構築
        for (int i = _receivedFiles.Count - 1; i >= 0; i--)
        {
            if (!File.Exists(_receivedFiles[i].FullPath))
            {
                _receivedFiles.RemoveAt(i);
            }
        }
        RebuildReceivedFilesList();
    }

    /// <summary>
    /// 「履歴クリア」ボタン：画面上の受信履歴だけを空にします（ファイル自体は消しません）。
    /// </summary>
    private void OnClearListClicked(object? sender, EventArgs e)
    {
        _receivedFiles.Clear();
        RebuildReceivedFilesList();
    }

    #endregion

    #region Shared Files Management (Download Panel)

    /// <summary>
    /// 送信用フォルダーの中身を読み込んで共有ファイル一覧を更新します。
    /// </summary>
    private void LoadSharedFiles()
    {
        _sharedFiles.Clear();
        string folder = StorageSettings.GetDownloadFolder();

        if (Directory.Exists(folder))
        {
            try
            {
                var di = new DirectoryInfo(folder);
                foreach (var fi in di.GetFiles().OrderByDescending(f => f.LastWriteTime))
                {
                    _sharedFiles.Add(new SharedFileInfo
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
                Debug.WriteLine($"[MainPage] LoadSharedFiles error: {ex.Message}");
            }
        }

        RebuildSharedFilesList();
    }

    /// <summary>
    /// 共有ファイル一覧の「更新」ボタンが押されたときの処理です。
    /// </summary>
    private void OnRefreshSharedFilesClicked(object? sender, EventArgs e)
    {
        LoadSharedFiles();
    }

    /// <summary>
    /// スマホ向けに公開しているファイル一覧の行（開くボタン・削除ボタン付き）を画面に並べます。
    /// </summary>
    private void RebuildSharedFilesList()
    {
        SharedFilesStack.Children.Clear();
        if (_sharedFiles.Count == 0)
        {
            EmptySharedContainer.IsVisible = true;
            return;
        }

        EmptySharedContainer.IsVisible = false;
        var loc = LocalizationService.Instance;

        foreach (var fileInfo in _sharedFiles)
        {
            var rowBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 4)
            };
            rowBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E2E8F0"), Color.FromArgb("#334155"));
            rowBorder.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F8FAFC"), Color.FromArgb("#0F172A"));

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                ],
                ColumnSpacing = 8,
                VerticalOptions = LayoutOptions.Center
            };

            var iconLabel = new Label
            {
                Text = fileInfo.TypeIcon,
                FontSize = 18,
                VerticalOptions = LayoutOptions.Center
            };

            var textStack = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center
            };

            var nameLabel = new Label
            {
                Text = fileInfo.FileName,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.MiddleTruncation,
                VerticalOptions = LayoutOptions.Center
            };
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1E293B"), Color.FromArgb("#F8FAFC"));

            var subLabel = new Label
            {
                Text = $"{fileInfo.FormattedSize}  •  {fileInfo.FormattedTime}",
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            };
            subLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#64748B"), Color.FromArgb("#94A3B8"));

            textStack.Children.Add(nameLabel);
            textStack.Children.Add(subLabel);

            var openBtn = new Button
            {
                Text = loc.Get("OpenFileButton"),
                FontSize = 11.5,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 30,
                Padding = new Thickness(12, 0),
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#EEF2FF"),
                TextColor = Color.FromArgb("#4F46E5"),
                VerticalOptions = LayoutOptions.Center
            };
            openBtn.Clicked += (_, _) =>
            {
                try
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = fileInfo.FullPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainPage] Open shared file error: {ex.Message}");
                }
            };

            var deleteBtn = new Button
            {
                Text = "🗑️",
                FontSize = 12,
                HeightRequest = 30,
                WidthRequest = 34,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#FEE2E2"),
                TextColor = Color.FromArgb("#DC2626"),
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.Clicked += async (_, _) =>
            {
                try
                {
                    if (File.Exists(fileInfo.FullPath))
                    {
                        File.Delete(fileInfo.FullPath);
                        LoadSharedFiles();
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync(loc.Get("StatusError"), ex.Message, loc.Get("CloseButton"));
                }
            };

            grid.Add(iconLabel, 0, 0);
            grid.Add(textStack, 1, 0);
            grid.Add(openBtn, 2, 0);
            grid.Add(deleteBtn, 3, 0);

            rowBorder.Content = grid;
            SharedFilesStack.Children.Add(rowBorder);
        }
    }

    #endregion

    #region ℹ️ About Modal

    /// <summary>
    /// アプリ情報モーダルに表示するバージョン文字列をセットします。
    /// </summary>
    private void UpdateAboutVersion()
    {
        if (AboutVersionLabel == null) return;
        var loc = LocalizationService.Instance;
        var ver = !string.IsNullOrWhiteSpace(AppInfo.Current.VersionString)
            ? AppInfo.Current.VersionString
            : (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.1.1");
        AboutVersionLabel.Text = $"{loc.Get("VersionLabel")}: {ver}";
    }

    /// <summary>
    /// 右上の「ℹ️」ボタンが押されたときにアプリ情報ダイアログを開きます。
    /// </summary>
    private void OnAboutClicked(object? sender, EventArgs e)
    {
        UpdateAboutVersion();
        if (AboutModalOverlay != null)
        {
            AboutModalOverlay.IsVisible = true;
        }
    }

    /// <summary>
    /// アプリ情報ダイアログを閉じます。
    /// </summary>
    private void OnCloseAboutModalClicked(object? sender, EventArgs e)
    {
        if (AboutModalOverlay != null)
        {
            AboutModalOverlay.IsVisible = false;
        }
    }

    /// <summary>
    /// ホームページのリンクが押されたときにブラウザで開きます。
    /// </summary>
    private async void OnOpenHomepageClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync(new Uri("https://sites.google.com/view/kuma-applications/%E7%B0%A1%E5%8D%98%E3%83%95%E3%82%A1%E3%82%A4%E3%83%AB%E8%BB%A2%E9%80%81"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Failed to open homepage: {ex.Message}");
        }
    }

    /// <summary>
    /// Instagramのリンクが押されたときにブラウザで開きます。
    /// </summary>
    private async void OnOpenInstagramClicked(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync(new Uri("https://www.instagram.com/zefa_25_zefa/"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Failed to open Instagram: {ex.Message}");
        }
    }

    #endregion
}
