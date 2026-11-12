using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using EasyFileTransfer.Linux.ViewModels;

namespace EasyFileTransfer.Linux;

/// <summary>
/// ViewModelの名前から対応するViewを自動で見つけて表示してくれるクラスです（Avaloniaテンプレート標準の仕組み）。
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// ViewModel に対応する View コントロールを生成します。
    /// </summary>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// 渡されたデータが ViewModelBase を継承しているか判定します。
    /// </summary>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
