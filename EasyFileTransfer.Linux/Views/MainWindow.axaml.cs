using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using EasyFileTransfer.Linux.ViewModels;

namespace EasyFileTransfer.Linux.Views;

/// <summary>
/// Linux版メインウィンドウのコードビハインドです。
/// 画面側でしかできないフォルダー選択ダイアログやクリップボード処理を ViewModel に渡しています。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// コンストラクタ：ウィンドウ表示時と終了時のイベントを登録します。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnMainWindowLoaded;
        Closing += OnMainWindowClosing;
    }

    /// <summary>
    /// ウィンドウ読み込み完了時に、ファイル選択・フォルダー選択・コピー機能の処理をセットします。
    /// </summary>
    private void OnMainWindowLoaded(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // フォルダー選択ハンドラー
            vm.PickFolderHandler = async () =>
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = vm.Loc.Get("ChangeFolderButton"),
                    AllowMultiple = false
                });

                if (folders.Count > 0)
                {
                    return folders[0].TryGetLocalPath();
                }
                return null;
            };

            // ファイル選択ハンドラー
            vm.PickFilesHandler = async () =>
            {
                var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = vm.Loc.Get("AddFilesButton"),
                    AllowMultiple = true
                });

                return files.Select(f => f.TryGetLocalPath())
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Select(p => p!)
                            .ToArray();
            };

            // クリップボードハンドラー
            vm.SetClipboardTextHandler = async text =>
            {
                if (Clipboard != null)
                {
                    await Clipboard.SetTextAsync(text);
                }
            };
        }
    }

    /// <summary>
    /// ウィンドウを閉じるときに、裏で動いているサーバーの後片付け（Dispose）を呼びます。
    /// </summary>
    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Dispose();
        }
    }
}
