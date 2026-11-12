using Avalonia;
using System;

namespace EasyFileTransfer.Linux;

/// <summary>
/// Linux（Avalonia UI）版のアプリを起動するエントリーポイントクラスです。
/// </summary>
sealed class Program
{
    /// <summary>
    /// プログラム起動時に最初に呼ばれるメイン関数です。
    /// </summary>
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Avalonia アプリ全体の設定を組み立てます（UIデザイナーでも使われるそうです）。
    /// </summary>
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
