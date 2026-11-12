using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EasyFileTransfer.Linux.ViewModels;
using EasyFileTransfer.Linux.Views;

namespace EasyFileTransfer.Linux;

/// <summary>
/// Avalonia UI アプリ全体の初期化を担当するクラスです。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// App.axaml の見た目やリソースを読み込みます。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// フレームワークの初期化が終わったタイミングで、メインウィンドウを開いて ViewModel を紐づけます。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}