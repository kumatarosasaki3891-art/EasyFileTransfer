using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace EasyFileTransfer.Services;

/// <summary>
/// Linux / クロスプラットフォーム版向けのフォルダーパスと設定ファイル（config.json）を管理するクラスです。
/// MAUI版とメソッド名を合わせておいたので、FileTransferServer.cs をそのまま共有できています。
/// </summary>
public static class StorageSettings
{
    private static readonly string ConfigDir;
    private static readonly string ConfigFilePath;

    private static string? _currentTargetFolder;
    private static string? _currentDownloadFolder;

    /// <summary>
    /// config.json に読み書きする設定項目をまとめる内部クラスです。
    /// </summary>
    private sealed class AppConfig
    {
        public string? TargetFolder { get; set; }
        public string? DownloadFolder { get; set; }
        public string? Language { get; set; }
    }

    private static AppConfig _config = new();

    /// <summary>
    /// 静的コンストラクタ：OSごとの設定保存先を決めて config.json を読み込み、多言語サービスとつなぎます。
    /// </summary>
    static StorageSettings()
    {
        string baseDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        else
        {
            // XDG_CONFIG_HOME or ~/.config
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            baseDir = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }

        ConfigDir = Path.Combine(baseDir, "EasyFileTransfer");
        ConfigFilePath = Path.Combine(ConfigDir, "config.json");

        LoadConfig();

        // LocalizationService の設定ハンドラを登録
        LocalizationService.ExternalPreferenceGetter = (key, def) =>
        {
            if (key == "EasyFileTransferAppLanguage")
            {
                return _config.Language ?? def;
            }
            return def;
        };

        LocalizationService.ExternalPreferenceSetter = (key, value) =>
        {
            if (key == "EasyFileTransferAppLanguage")
            {
                _config.Language = value;
                SaveConfig();
            }
        };
    }

    /// <summary>
    /// config.json があれば読み込んで設定を復元します。
    /// </summary>
    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                    _currentTargetFolder = _config.TargetFolder;
                    _currentDownloadFolder = _config.DownloadFolder;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] LoadConfig error: {ex.Message}");
        }
    }

    /// <summary>
    /// 現在の設定を config.json にJSON形式で保存します。
    /// </summary>
    private static void SaveConfig()
    {
        try
        {
            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] SaveConfig error: {ex.Message}");
        }
    }

    /// <summary>
    /// スマホから受信したファイルを保存するフォルダーのパスを返します（初期値: ~/Downloads/EasyFileTransfer）。
    /// </summary>
    public static string GetUploadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_currentTargetFolder) && Directory.Exists(_currentTargetFolder))
        {
            return _currentTargetFolder;
        }

        if (!string.IsNullOrWhiteSpace(_config.TargetFolder) && Directory.Exists(_config.TargetFolder))
        {
            _currentTargetFolder = _config.TargetFolder;
            return _config.TargetFolder;
        }

        // デフォルト: ~/Downloads/EasyFileTransfer
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
    /// 受信用の保存先フォルダーを新しく設定して config.json に保存します。
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
            _config.TargetFolder = folderPath;
            SaveConfig();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error saving folder: {ex.Message}");
            _currentTargetFolder = folderPath;
        }
    }

    /// <summary>
    /// スマホにダウンロードして渡すファイルの共有フォルダーパスを返します（初期値: ~/Downloads/EasyFileTransfer/Shared）。
    /// </summary>
    public static string GetDownloadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_currentDownloadFolder) && Directory.Exists(_currentDownloadFolder))
        {
            return _currentDownloadFolder;
        }

        if (!string.IsNullOrWhiteSpace(_config.DownloadFolder) && Directory.Exists(_config.DownloadFolder))
        {
            _currentDownloadFolder = _config.DownloadFolder;
            return _config.DownloadFolder;
        }

        // デフォルト: ~/Downloads/EasyFileTransfer/Shared
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
    /// 共有フォルダーを新しく設定して config.json に保存します。
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
            _config.DownloadFolder = folderPath;
            SaveConfig();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error saving download folder: {ex.Message}");
            _currentDownloadFolder = folderPath;
        }
    }

    /// <summary>
    /// OSごとのファイラー（Linuxならxdg-open、Windowsならexplorerなど）でフォルダーを開きます。
    /// </summary>
    public static void OpenInExplorer(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{folderPath}\"");
            }
            else
            {
                // Linux / Unix
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error opening folder: {ex.Message}");
        }
    }

    /// <summary>
    /// 指定されたファイルをOS既定のアプリで開きます。
    /// </summary>
    public static void OpenFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{filePath}\"");
            }
            else
            {
                // Linux / Unix
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] Error opening file: {ex.Message}");
        }
    }
}
