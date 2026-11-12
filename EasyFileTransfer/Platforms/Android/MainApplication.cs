using Android.App;
using Android.Runtime;

namespace EasyFileTransfer
{
    /// <summary>
    /// Android版のアプリケーション本体クラスです。（MAUIテンプレートのまま残してあります）
    /// </summary>
    [Application]
    public class MainApplication : MauiApplication
    {
        /// <summary>
        /// コンストラクタです。
        /// </summary>
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        /// <summary>
        /// Android側のMAUIアプリを生成します。
        /// </summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
