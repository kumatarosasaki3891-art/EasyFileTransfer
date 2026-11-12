using Foundation;

namespace EasyFileTransfer
{
    /// <summary>
    /// MacCatalyst版のアプリケーション開始処理をまとめるクラスです。（テンプレートのまま残しています）
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /// <summary>
        /// MacCatalyst向けのMAUIアプリを作ります。
        /// </summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
