using System.Text;
using EasyFileTransfer.Services;

namespace EasyFileTransfer.Cli.Services;

/// <summary>
/// QrCodeGenerator.EncodeToMatrix の出力を半ブロック文字（上下2行圧縮）で端末に描画します。
/// </summary>
public static class TerminalQrRenderer
{
    public static string RenderToTerminal(string text, bool useAnsiColor = true, int quietZoneModules = 4)
    {
        var matrix = QrCodeGenerator.EncodeToMatrix(text);
        int size = matrix.GetLength(0);
        int total = size + quietZoneModules * 2;
        var sb = new StringBuilder();

        for (int row = 0; row < total; row += 2)
        {
            if (useAnsiColor)
            {
                // 前景: 黒(30), 背景: 白(47) で端末のダーク/ライト配色に左右されないようにする
                sb.Append("\u001b[30;47m");
            }

            for (int col = 0; col < total; col++)
            {
                bool top = IsDark(matrix, size, quietZoneModules, row, col);
                bool bottom = row + 1 < total && IsDark(matrix, size, quietZoneModules, row + 1, col);
                char ch = (top, bottom) switch
                {
                    (true, true) => '\u2588',   // █
                    (true, false) => '\u2580',  // ▀
                    (false, true) => '\u2584',  // ▄
                    (false, false) => ' '
                };
                sb.Append(ch);
            }

            if (useAnsiColor)
            {
                sb.Append("\u001b[0m");
            }

            if (row + 2 < total)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static bool IsDark(bool[,] matrix, int size, int quiet, int row, int col)
    {
        int r = row - quiet;
        int c = col - quiet;
        return r >= 0 && r < size && c >= 0 && c < size && matrix[r, c];
    }
}
