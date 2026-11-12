namespace EasyFileTransfer.Models;

/// <summary>
/// スマホにダウンロードしてあげるPC側ファイルの情報を入れておくクラス（record）です。
/// ReceivedFileInfo とほとんど同じ作りにしています。
/// </summary>
public sealed record SharedFileInfo
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime LastModifiedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// 拡張子を小文字で取り出します
    /// </summary>
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    /// <summary>
    /// バイト数を B / KB / MB / GB の分かりやすい表記に変換します
    /// </summary>
    public string FormattedSize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    /// <summary>
    /// ファイルの更新日時を表示用の文字列にします
    /// </summary>
    public string FormattedTime => LastModifiedAt.ToString("yyyy/MM/dd HH:mm");

    /// <summary>
    /// 拡張子に応じた絵文字アイコンを返します
    /// </summary>
    public string TypeIcon => Extension switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" or ".heic" => "🖼️",
        ".pdf" => "📕",
        ".json" => "⚙️",
        ".csv" or ".tsv" => "📊",
        ".gpx" or ".kml" => "🗺️",
        ".zip" or ".7z" or ".tar" or ".gz" or ".rar" => "📦",
        ".txt" or ".md" or ".log" => "📝",
        ".mp4" or ".mov" or ".avi" or ".mkv" => "🎬",
        ".mp3" or ".m4a" or ".wav" or ".flac" => "🎵",
        _ => "📄"
    };
}
