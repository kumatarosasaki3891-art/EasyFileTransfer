using System.Diagnostics;

namespace EasyFileTransfer.Services;

/// <summary>
/// 受信ファイルの保存先と、スマホへ共有するフォルダーのパスを管理するクラスです。
/// 選択したフォルダーはPreferencesに保存して、次回起動時にも覚えているようにしています。
/// </summary>
public static class StorageSettings
{
    private const string TargetFolderPrefKey = "EasyFileTransfer_TargetFolderPath";
    private const string DownloadFolderPrefKey = "EasyFileTransfer_DownloadFolderPath";
    private static string? _currentTargetFolder;
    private static string? _currentDownloadFolder;

    /// <summary>
    /// 受信用の保存先フォルダーを取得します（古い名前との互換用に残してあります）。
    /// </summary>
    public static string GetTargetFolder() => GetUploadFolder();

    /// <summary>
    /// 受信用の保存先フォルダーを設定します（互換用のラッパーメソッドです）。
    /// </summary>
    public static void SetTargetFolder(string folderPath) => SetUploadFolder(folderPath);

    /// <summary>
    /// スマホから受信したファイルを保存するフォルダーのパスを返します。
    /// まだ設定されていないときや、フォルダーが消えてしまっていたときは「Downloads\EasyFileTransfer」を作ります。
    /// </summary>
    public static string GetUploadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_currentTargetFolder) && Directory.Exists(_currentTargetFolder))
        {
            return _currentTargetFolder;
        }

        string? saved = null;
        try
        {
            saved = Preferences.Default.Get(TargetFolderPrefKey, string.Empty);
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
        {
            _currentTargetFolder = saved;
            return saved;
        }

        // デフォルトフォルダー: Downloads\EasyFileTransfer
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultDownloads = Path.Combine(userProfile, "Downloads", "EasyFileTransfer");

        try
        {
            if (!Directory.Exists(defaultDownloads))
            {
                Directory.CreateDirectory(defaultDownloads);
            }
            _currentTargetFolder = defaultDownloads;
            return defaultDownloads;
        }
        catch
        {
            // フォールバック: MyDocuments
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EasyFileTransfer");
            if (!Directory.Exists(fallback))
            {
                Directory.CreateDirectory(fallback);
            }
            _currentTargetFolder = fallback;
            return fallback;
        }
    }

    /// <summary>
    /// スマホから送られてくるファイルの保存先フォルダーを変更して保存します。
    /// </summary>
    public static void SetUploadFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            _currentTargetFolder = folderPath;
            Preferences.Default.Set(TargetFolderPrefKey, folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error saving folder: {ex.Message}");
            _currentTargetFolder = folderPath;
        }
    }

    /// <summary>
    /// スマホ側へ送る（共有する）PC上のフォルダーパスを返します。
    /// 初期状態では「Downloads\EasyFileTransfer\Shared」を使うようにしています。
    /// </summary>
    public static string GetDownloadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_currentDownloadFolder) && Directory.Exists(_currentDownloadFolder))
        {
            return _currentDownloadFolder;
        }

        string? saved = null;
        try
        {
            saved = Preferences.Default.Get(DownloadFolderPrefKey, string.Empty);
        }
        catch { }

        if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved))
        {
            _currentDownloadFolder = saved;
            return saved;
        }

        // デフォルトフォルダー: Downloads\EasyFileTransfer\Shared
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultShared = Path.Combine(userProfile, "Downloads", "EasyFileTransfer", "Shared");

        try
        {
            if (!Directory.Exists(defaultShared))
            {
                Directory.CreateDirectory(defaultShared);
            }
            _currentDownloadFolder = defaultShared;
            return defaultShared;
        }
        catch
        {
            // フォールバック: MyDocuments
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EasyFileTransfer", "Shared");
            if (!Directory.Exists(fallback))
            {
                Directory.CreateDirectory(fallback);
            }
            _currentDownloadFolder = fallback;
            return fallback;
        }
    }

    /// <summary>
    /// スマホへ共有するフォルダーの場所を新しく保存します。
    /// </summary>
    public static void SetDownloadFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return;

        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            _currentDownloadFolder = folderPath;
            Preferences.Default.Set(DownloadFolderPrefKey, folderPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error saving download folder: {ex.Message}");
            _currentDownloadFolder = folderPath;
        }
    }

    /// <summary>
    /// 指定したフォルダーをエクスプローラーで開きます（存在しない場合は先に作成します）。
    /// </summary>
    public static void OpenInExplorer(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error opening explorer: {ex.Message}");
        }
    }

    /// <summary>
    /// Windows標準のフォルダー選択ダイアログを開いて、選ばれたパスを返します。
    /// WinUI 3 のウィンドウハンドル紐付けが少しややこしく、調べながら書いています。
    /// </summary>
    public static async Task<string?> PickFolderAsync()
    {
#if WINDOWS
        try
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.FileTypeFilter.Add("*");

            // WinUI 3 Window ハンドルの取得
            var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (window != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);
            }

            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] PickFolderAsync error: {ex.Message}");
            return null;
        }
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    /// <summary>
    /// ファイル選択ダイアログで複数ファイルを選んでもらい、共有用フォルダーへコピーします。
    /// コピーできた件数を返します。
    /// </summary>
    public static async Task<int> PickAndCopyFilesToFolderAsync(string targetFolder)
    {
        try
        {
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = LocalizationService.Instance.Get("SelectFilesToShareTitle")
            });

            if (results == null) return 0;

            int copiedCount = 0;
            foreach (var file in results)
            {
                if (file == null || string.IsNullOrWhiteSpace(file.FileName)) continue;
                string destFile = Path.Combine(targetFolder, file.FileName);
                // 同名ファイルがある場合は上書きまたは別名
                await using var sourceStream = await file.OpenReadAsync();
                await using var destStream = File.Create(destFile);
                await sourceStream.CopyToAsync(destStream);
                copiedCount++;
            }

            return copiedCount;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] PickAndCopyFilesToFolderAsync error: {ex.Message}");
            return 0;
        }
    }
}
