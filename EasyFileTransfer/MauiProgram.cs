using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace EasyFileTransfer
{
    /// <summary>
    /// .NET MAUI アプリの起動時に最初に設定を読み込むクラスです。
    /// </summary>
    public static class MauiProgram
    {
        /// <summary>
        /// アプリのビルダーを作って、フォントなどを登録します。
        /// </summary>
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if WINDOWS
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(windows => windows.OnWindowCreated(window =>
                {
                    var hwnd = WindowNative.GetWindowHandle(window);
                    var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = AppWindow.GetFromWindowId(windowId);
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                    if (File.Exists(iconPath))
                    {
                        appWindow.SetIcon(iconPath);
                    }
                }));
            });
#endif

#if DEBUG
            // デバッグ実行時だけログ出力を有効にしています
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
