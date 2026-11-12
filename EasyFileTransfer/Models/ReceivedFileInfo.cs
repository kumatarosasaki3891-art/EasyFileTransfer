namespace EasyFileTransfer.Models;

/// <summary>
/// スマホ等から転送・保存されたファイルの情報をまとめておくクラス（record）です。
/// 一覧に表示するときのサイズ表示やアイコンもここで作っています。
/// </summary>
public sealed record ReceivedFileInfo
{
    public required string FileName { get; init; }
    public required string FullPath { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime ReceivedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// 拡張子を小文字で取り出します（アイコン判定用）
    /// </summary>
    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

    /// <summary>
    /// バイト数を B / KB / MB / GB の見やすい文字列に変換します
    /// </summary>
    public string FormattedSize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
    };

    /// <summary>
    /// 受信した時刻を「時:分:秒」で返します
    /// </summary>
    public string FormattedTime => ReceivedAt.ToString("HH:mm:ss");

    /// <summary>
    /// ファイルの拡張子に合わせて、ちょっとした絵文字アイコンを選びます
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
