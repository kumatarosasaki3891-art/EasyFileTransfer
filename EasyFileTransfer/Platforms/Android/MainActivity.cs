using Android.App;
using Android.Content.PM;
using Android.OS;

namespace EasyFileTransfer
{
    /// <summary>
    /// Android版のメイン画面（Activity）クラスです。（今はWindowsメインで作っているのでテンプレートのままです）
    /// </summary>
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
    }
}
