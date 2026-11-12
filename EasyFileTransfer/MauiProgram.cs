using Microsoft.Extensions.Logging;

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

#if DEBUG
            // デバッグ実行時だけログ出力を有効にしています
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
