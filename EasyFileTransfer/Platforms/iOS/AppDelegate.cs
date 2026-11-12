using Foundation;

namespace EasyFileTransfer
{
    /// <summary>
    /// iOS版のアプリケーション開始処理をまとめるクラスです。（現在はテンプレートのままです）
    /// </summary>
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /// <summary>
        /// iOS向けのMAUIアプリを作ります。
        /// </summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
