using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace EasyFileTransfer.Services;

/// <summary>
/// CLI版向けの保存先フォルダー・設定ファイル（config.json）管理クラスです。
/// GUI（Linux版）と同じ config.json を参照しつつ、CLI引数での変更は実行中のみ有効にします。
/// </summary>
public static class StorageSettings
{
    private static readonly string ConfigDir;
    public static string ConfigFilePath { get; }

    private static string? _sessionUploadOverride;
    private static string? _sessionDownloadOverride;
    private static AppConfig _config = new();

    private sealed class AppConfig
    {
        public string? TargetFolder { get; set; }
        public string? DownloadFolder { get; set; }
        public string? Language { get; set; }
    }

    static StorageSettings()
    {
        string baseDir;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA");
            baseDir = !string.IsNullOrWhiteSpace(appData)
                ? appData
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                baseDir = xdg;
            }
            else
            {
                var home = GetUserHomeDirectory();
                baseDir = string.IsNullOrWhiteSpace(home)
                    ? Environment.CurrentDirectory
                    : Path.Combine(home, ".config");
            }
        }

        ConfigDir = Path.Combine(baseDir, "EasyFileTransfer");
        ConfigFilePath = Path.Combine(ConfigDir, "config.json");

        LoadConfig();

        LocalizationService.ExternalPreferenceGetter = (key, def) =>
        {
            if (key == "EasyFileTransferAppLanguage")
            {
                return _config.Language ?? def;
            }
            return def;
        };

        // CLI起動時の言語指定は設定ファイルを上書きしない
        LocalizationService.ExternalPreferenceSetter = (_, _) => { };
    }

    /// <summary>
    /// 静的コンストラクタを明示的に実行し、LocalizationService.Instance 生成前に設定を接続します。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Initialize()
    {
        RuntimeHelpers.RunClassConstructor(typeof(StorageSettings).TypeHandle);
    }

    /// <summary>
    /// 実行中のみ有効な受信フォルダーの上書きを設定します。
    /// </summary>
    public static void SetSessionUploadFolderOverride(string? folderPath)
    {
        _sessionUploadOverride = string.IsNullOrWhiteSpace(folderPath) ? null : folderPath;
    }

    /// <summary>
    /// 実行中のみ有効な共有フォルダーの上書きを設定します。
    /// </summary>
    public static void SetSessionDownloadFolderOverride(string? folderPath)
    {
        _sessionDownloadOverride = string.IsNullOrWhiteSpace(folderPath) ? null : folderPath;
    }

    /// <summary>
    /// 実行中のセッション上書き設定をすべて解除します。
    /// </summary>
    public static void ResetOverrides()
    {
        _sessionUploadOverride = null;
        _sessionDownloadOverride = null;
    }

    /// <summary>
    /// スマホから受信したファイルの保存先フォルダーを返します。
    /// </summary>
    public static string GetUploadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_sessionUploadOverride))
        {
            Directory.CreateDirectory(_sessionUploadOverride);
            return _sessionUploadOverride;
        }

        if (!string.IsNullOrWhiteSpace(_config.TargetFolder) && Directory.Exists(_config.TargetFolder))
        {
            return _config.TargetFolder;
        }

        return ResolveDefaultFolder(isShared: false);
    }

    /// <summary>
    /// スマホへ共有するファイルのフォルダーを返します。
    /// </summary>
    public static string GetDownloadFolder()
    {
        if (!string.IsNullOrWhiteSpace(_sessionDownloadOverride))
        {
            return _sessionDownloadOverride;
        }

        if (!string.IsNullOrWhiteSpace(_config.DownloadFolder) && Directory.Exists(_config.DownloadFolder))
        {
            return _config.DownloadFolder;
        }

        return ResolveDefaultFolder(isShared: true);
    }

    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (loaded != null)
                {
                    _config = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageSettings] LoadConfig error: {ex.Message}");
        }
    }

    private static string ResolveDefaultFolder(bool isShared)
    {
        var candidates = new List<string>();

        var userProfile = GetUserHomeDirectory();
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            candidates.Add(isShared
                ? Path.Combine(userProfile, "Downloads", "EasyFileTransfer", "Shared")
                : Path.Combine(userProfile, "Downloads", "EasyFileTransfer"));
        }

        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(myDocs))
        {
            candidates.Add(isShared
                ? Path.Combine(myDocs, "EasyFileTransfer", "Shared")
                : Path.Combine(myDocs, "EasyFileTransfer"));
        }

        candidates.Add(isShared
            ? Path.Combine(Environment.CurrentDirectory, "EasyFileTransfer", "Shared")
            : Path.Combine(Environment.CurrentDirectory, "EasyFileTransfer"));

        Exception? lastError = null;
        foreach (var path in candidates)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new IOException($"Failed to resolve or create default directory: {lastError?.Message}", lastError);
    }

    private static string GetUserHomeDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            return profile;
        }
        return Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
    }
}
