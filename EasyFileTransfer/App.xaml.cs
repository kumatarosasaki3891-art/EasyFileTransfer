namespace EasyFileTransfer;

/// <summary>
/// アプリ全体の本体になるクラスです。
/// 起動したときの最初の画面やウィンドウサイズなどをここで設定しています。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// コンストラクタ：XAML側の初期化を呼び出します。
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// アプリが起動するときにメインウィンドウを作るメソッドです。
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // 横幅・高さの初期サイズと、これ以上小さくできない最小サイズを指定しています
        var window = new Window(new AppShell())
        {
            Title = "EasyFileTransfer",
            Width = 1060,
            Height = 720,
            MinimumWidth = 850,
            MinimumHeight = 560
        };
        return window;
    }
}
