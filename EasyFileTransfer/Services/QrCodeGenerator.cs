using System.Text;
#if !CLI
using SkiaSharp;
#endif

namespace EasyFileTransfer.Services;

/// <summary>
/// 外部のQR生成ライブラリを増やさずに動かしたかったので、SkiaSharpだけでQRコードを描くクラスを作りました。
/// ISO/IEC 18004 の仕様に沿って、ローカル転送用URLのQRコード画像（PNG / ImageSource）を作ります。
/// </summary>
public static class QrCodeGenerator
{
    /// <summary>
    /// QRコードのバージョンごとのサイズ・誤り訂正レベル・位置合わせパターンの座標をまとめる構造体です
    /// </summary>
    private readonly record struct VersionSpec(
        int Version,
        int EccFormatBits, // L = 0b01, M = 0b00
        int TotalCodewords,
        int DataCodewords,
        int EcCodewords,
        int[] AlignmentCenters);

    // URL長（最大200バイト以上）まで対応可能な Version 1〜7 定義
    private static readonly VersionSpec[] Specs =
    [
        new(1, 0b00, 26, 16, 10, []),          // Version 1-M (最大 14 bytes)
        new(2, 0b00, 44, 28, 16, [6, 18]),     // Version 2-M (最大 26 bytes)
        new(3, 0b00, 70, 44, 26, [6, 22]),     // Version 3-M (最大 42 bytes)
        new(3, 0b01, 70, 55, 15, [6, 22]),     // Version 3-L (最大 53 bytes)
        new(4, 0b01, 100, 80, 20, [6, 26]),    // Version 4-L (最大 78 bytes)
        new(5, 0b01, 134, 108, 26, [6, 30]),   // Version 5-L (最大 106 bytes)
        new(6, 0b01, 172, 136, 36, [6, 34]),   // Version 6-L (最大 134 bytes)
        new(7, 0b01, 196, 156, 40, [6, 22, 38])// Version 7-L (最大 154 bytes)
    ];

#if !CLI
    /// <summary>
    /// 渡されたテキスト（URLなど）をQRコードのPNG画像（バイト配列）にして返します。
    /// Linux版(Avalonia)などでもそのまま使えるようにしています。
    /// </summary>
    public static byte[] GeneratePngBytes(string text, int pixelsPerModule = 10, int quietZoneModules = 4)
    {
        var matrix = EncodeToMatrix(text);
        int size = matrix.GetLength(0);
        int totalModules = size + quietZoneModules * 2;
        int imgSize = totalModules * pixelsPerModule;

        using var surface = SKSurface.Create(new SKImageInfo(imgSize, imgSize, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var blackPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = false
        };

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (matrix[r, c])
                {
                    float x = (c + quietZoneModules) * pixelsPerModule;
                    float y = (r + quietZoneModules) * pixelsPerModule;
                    canvas.DrawRect(x, y, pixelsPerModule, pixelsPerModule, blackPaint);
                }
            }
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
#endif

#if !AVALONIA && !CLI
    /// <summary>
    /// MAUIの Image コントロールにそのまま貼れる ImageSource としてQRコードを生成します。
    /// </summary>
    public static ImageSource GenerateImageSource(string text, int pixelsPerModule = 10)
    {
        var pngBytes = GeneratePngBytes(text, pixelsPerModule);
        return ImageSource.FromStream(() => new MemoryStream(pngBytes));
    }
#endif

    /// <summary>
    /// 文字列をQRコードの白黒マス目（2次元bool配列）に変換するメイン処理です。
    /// </summary>
    public static bool[,] EncodeToMatrix(string text)
    {
        var payload = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var spec = SelectSpec(payload.Length);

        var dataCodewords = BuildDataCodewords(payload, spec.DataCodewords);
        var ecCodewords = ComputeReedSolomon(dataCodewords, spec.EcCodewords);

        var allCodewords = new byte[spec.TotalCodewords];
        Array.Copy(dataCodewords, 0, allCodewords, 0, dataCodewords.Length);
        Array.Copy(ecCodewords, 0, allCodewords, dataCodewords.Length, ecCodewords.Length);

        int size = 17 + 4 * spec.Version;
        var baseMatrix = new bool[size, size];
        var isFunction = new bool[size, size];

        PlaceFunctionPatterns(baseMatrix, isFunction, spec);
        PlaceDataBits(baseMatrix, isFunction, allCodewords);

        // 8種のマスクパターンからペナルティ最小のものを選択
        int bestMask = 0;
        int minPenalty = int.MaxValue;
        bool[,]? bestMatrix = null;

        for (int mask = 0; mask < 8; mask++)
        {
            var candidate = (bool[,])baseMatrix.Clone();
            ApplyMask(candidate, isFunction, mask);
            WriteFormatBits(candidate, spec.EccFormatBits, mask);

            int penalty = CalculatePenalty(candidate);
            if (penalty < minPenalty)
            {
                minPenalty = penalty;
                bestMask = mask;
                bestMatrix = candidate;
            }
        }

        return bestMatrix!;
    }

    /// <summary>
    /// データの長さ（バイト数）に収まる最小のQRコードバージョンを選びます。
    /// </summary>
    private static VersionSpec SelectSpec(int byteLength)
    {
        foreach (var spec in Specs)
        {
            // Byte mode header: 4 bits (mode 0100) + 8 bits (length) = 12 bits -> 2 bytes overhead
            if (byteLength + 2 <= spec.DataCodewords)
            {
                return spec;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(byteLength), "QRコードに格納可能な最大文字数を超えています。");
    }

    /// <summary>
    /// 入力データの先頭にモード情報や文字数を付けて、規定サイズのバイト列（データコード語）に並べます。
    /// </summary>
    private static byte[] BuildDataCodewords(byte[] payload, int dataCodewordsCount)
    {
        int capacityBits = dataCodewordsCount * 8;
        var bits = new List<bool>(capacityBits);

        // 1. Mode indicator: Byte mode = 0100 (4 bits)
        AppendBits(bits, 0b0100, 4);

        // 2. Character count indicator (8 bits for Version 1..9 Byte mode)
        AppendBits(bits, payload.Length, 8);

        // 3. Payload bytes
        foreach (var b in payload)
        {
            AppendBits(bits, b, 8);
        }

        // 4. Terminator (up to 4 zero bits)
        int terminatorLen = Math.Min(4, capacityBits - bits.Count);
        AppendBits(bits, 0, terminatorLen);

        // 5. Pad to byte boundary
        while (bits.Count % 8 != 0)
        {
            bits.Add(false);
        }

        // 6. Pad codewords (0xEC, 0x11 alternating)
        byte[] padBytes = [0xEC, 0x11];
        int padIdx = 0;
        while (bits.Count < capacityBits)
        {
            AppendBits(bits, padBytes[padIdx % 2], 8);
            padIdx++;
        }

        var result = new byte[dataCodewordsCount];
        for (int i = 0; i < dataCodewordsCount; i++)
        {
            int val = 0;
            for (int b = 0; b < 8; b++)
            {
                val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
            }
            result[i] = (byte)val;
        }
        return result;
    }

    /// <summary>
    /// 数値の上位ビットから順番にビット列リストへ追加していく補助関数です。
    /// </summary>
    private static void AppendBits(List<bool> bits, int value, int bitCount)
    {
        for (int i = bitCount - 1; i >= 0; i--)
        {
            bits.Add(((value >> i) & 1) != 0);
        }
    }

    /// <summary>
    /// QRコードが一部汚れても読めるようにする、リード・ソロモン誤り訂正符号を計算します。
    /// ちょっと数式が難しかったので、GF(2^8)（原始多項式 0x11D）の仕様を見ながら実装しました。
    /// </summary>
    private static byte[] ComputeReedSolomon(byte[] data, int ecCount)
    {
        var generator = BuildGeneratorPolynomial(ecCount);
        var remainder = new byte[ecCount];

        foreach (var b in data)
        {
            byte factor = (byte)(b ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, ecCount - 1);
            remainder[ecCount - 1] = 0;

            if (factor != 0)
            {
                for (int i = 0; i < ecCount; i++)
                {
                    remainder[i] ^= GfMultiply(generator[i], factor);
                }
            }
        }

        return remainder;
    }

    /// <summary>
    /// 誤り訂正符号の割り算に使う生成多項式を作ります。
    /// </summary>
    private static byte[] BuildGeneratorPolynomial(int degree)
    {
        var poly = new byte[degree];
        poly[degree - 1] = 1;
        byte root = 1;

        for (int i = 0; i < degree; i++)
        {
            for (int j = 0; j < degree; j++)
            {
                poly[j] = GfMultiply(poly[j], root);
                if (j + 1 < degree)
                {
                    poly[j] ^= poly[j + 1];
                }
            }
            root = GfMultiply(root, 0x02);
        }

        return poly;
    }

    /// <summary>
    /// ガロア体 GF(2^8) 上での掛け算を行う関数です。
    /// </summary>
    private static byte GfMultiply(byte x, byte y)
    {
        int z = 0;
        for (int i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return (byte)z;
    }

    /// <summary>
    /// 3隅の切り出しシンボルやタイミングパターンなど、データ以外の固定パーツを盤面に配置します。
    /// </summary>
    private static void PlaceFunctionPatterns(bool[,] matrix, bool[,] isFunction, VersionSpec spec)
    {
        int size = matrix.GetLength(0);

        // 1. Finder patterns + separators (Top-Left, Top-Right, Bottom-Left)
        PlaceFinderPattern(matrix, isFunction, 0, 0);
        PlaceFinderPattern(matrix, isFunction, 0, size - 7);
        PlaceFinderPattern(matrix, isFunction, size - 7, 0);

        // 2. Timing patterns (row 6 and col 6)
        for (int i = 8; i < size - 8; i++)
        {
            bool dark = (i % 2 == 0);
            if (!isFunction[6, i])
            {
                matrix[6, i] = dark;
                isFunction[6, i] = true;
            }
            if (!isFunction[i, 6])
            {
                matrix[i, 6] = dark;
                isFunction[i, 6] = true;
            }
        }

        // 3. Alignment patterns
        var centers = spec.AlignmentCenters;
        for (int i = 0; i < centers.Length; i++)
        {
            for (int j = 0; j < centers.Length; j++)
            {
                // Skip if overlapping with finder patterns
                if ((i == 0 && j == 0) ||
                    (i == 0 && j == centers.Length - 1) ||
                    (i == centers.Length - 1 && j == 0))
                {
                    continue;
                }
                PlaceAlignmentPattern(matrix, isFunction, centers[i], centers[j]);
            }
        }

        // 4. Dark module and reserved format information areas
        matrix[size - 8, 8] = true;
        isFunction[size - 8, 8] = true;

        for (int i = 0; i < 9; i++)
        {
            isFunction[8, i] = true;
            isFunction[i, 8] = true;
        }
        for (int i = 0; i < 8; i++)
        {
            isFunction[8, size - 1 - i] = true;
            isFunction[size - 1 - i, 8] = true;
        }
    }

    /// <summary>
    /// 四隅（右下を除く3か所）にある「回」のような切り出しシンボルを書き込みます。
    /// </summary>
    private static void PlaceFinderPattern(bool[,] matrix, bool[,] isFunction, int topRow, int leftCol)
    {
        int size = matrix.GetLength(0);
        for (int dr = -1; dr <= 7; dr++)
        {
            for (int dc = -1; dc <= 7; dc++)
            {
                int r = topRow + dr;
                int c = leftCol + dc;
                if (r < 0 || r >= size || c < 0 || c >= size) continue;

                bool dark = (dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6) &&
                            (dr == 0 || dr == 6 || dc == 0 || dc == 6 ||
                             (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4));

                matrix[r, c] = dark;
                isFunction[r, c] = true;
            }
        }
    }

    /// <summary>
    /// バージョン2以上で登場する小さい四角（アライメントパターン）を書き込みます。
    /// </summary>
    private static void PlaceAlignmentPattern(bool[,] matrix, bool[,] isFunction, int centerRow, int centerCol)
    {
        for (int dr = -2; dr <= 2; dr++)
        {
            for (int dc = -2; dc <= 2; dc++)
            {
                int r = centerRow + dr;
                int c = centerCol + dc;
                bool dark = Math.Max(Math.Abs(dr), Math.Abs(dc)) != 1;
                matrix[r, c] = dark;
                isFunction[r, c] = true;
            }
        }
    }

    /// <summary>
    /// 右下からジグザグに上り下りしながら、実際のデータビットを空きマスに埋めていきます。
    /// </summary>
    private static void PlaceDataBits(bool[,] matrix, bool[,] isFunction, byte[] codewords)
    {
        int size = matrix.GetLength(0);
        int bitIdx = 0;
        int totalBits = codewords.Length * 8;
        bool upward = true;

        for (int rightCol = size - 1; rightCol >= 1; rightCol -= 2)
        {
            if (rightCol == 6) rightCol = 5; // Skip vertical timing pattern column

            for (int vert = 0; vert < size; vert++)
            {
                int r = upward ? (size - 1 - vert) : vert;
                for (int j = 0; j < 2; j++)
                {
                    int c = rightCol - j;
                    if (!isFunction[r, c])
                    {
                        bool dark = false;
                        if (bitIdx < totalBits)
                        {
                            dark = ((codewords[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                            bitIdx++;
                        }
                        matrix[r, c] = dark;
                    }
                }
            }
            upward = !upward;
        }
    }

    /// <summary>
    /// 白や黒ばかり偏らないように、指定されたパターン（0〜7）で白黒を反転（マスク）させます。
    /// </summary>
    private static void ApplyMask(bool[,] matrix, bool[,] isFunction, int mask)
    {
        int size = matrix.GetLength(0);
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (isFunction[r, c]) continue;

                bool invert = mask switch
                {
                    0 => (r + c) % 2 == 0,
                    1 => r % 2 == 0,
                    2 => c % 3 == 0,
                    3 => (r + c) % 3 == 0,
                    4 => ((r / 2) + (c / 3)) % 2 == 0,
                    5 => ((r * c) % 2) + ((r * c) % 3) == 0,
                    6 => (((r * c) % 2) + ((r * c) % 3)) % 2 == 0,
                    7 => (((r + c) % 2) + ((r * c) % 3)) % 2 == 0,
                    _ => false
                };

                if (invert)
                {
                    matrix[r, c] = !matrix[r, c];
                }
            }
        }
    }

    /// <summary>
    /// 選んだ誤り訂正レベルとマスク番号の情報を、切り出しシンボルの周りに書き込みます。
    /// </summary>
    private static void WriteFormatBits(bool[,] matrix, int eccFormatBits, int mask)
    {
        int size = matrix.GetLength(0);
        int data = (eccFormatBits << 3) | mask;
        int rem = data;
        for (int i = 0; i < 10; i++)
        {
            rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        }
        int bits = ((data << 10) | rem) ^ 0x5412;

        // Top-left copy (ISO/IEC 18004 Figure 25: bit 0 at (0,8) .. bit 14 at (8,0))
        for (int i = 0; i <= 5; i++) matrix[i, 8] = GetBit(bits, i);
        matrix[7, 8] = GetBit(bits, 6);
        matrix[8, 8] = GetBit(bits, 7);
        matrix[8, 7] = GetBit(bits, 8);
        for (int i = 9; i < 15; i++) matrix[8, 14 - i] = GetBit(bits, i);

        // Top-right (bit 0..7) and bottom-left (bit 8..14) copy
        for (int i = 0; i < 8; i++) matrix[8, size - 1 - i] = GetBit(bits, i);
        for (int i = 8; i < 15; i++) matrix[size - 15 + i, 8] = GetBit(bits, i);
        matrix[size - 8, 8] = true; // Dark module
    }

    /// <summary>
    /// 整数値の指定したビット位置が1かどうかを調べるミニ関数です。
    /// </summary>
    private static bool GetBit(int val, int index) => ((val >> index) & 1) != 0;

    /// <summary>
    /// どのマスクが一番カメラで読み取りやすいか（同色の連続や2x2ブロックが少ないか）を点数化して評価します。
    /// 点数が低いほど良いパターンです。
    /// </summary>
    private static int CalculatePenalty(bool[,] matrix)
    {
        int size = matrix.GetLength(0);
        int penalty = 0;

        // Rule 1: 5+ consecutive modules of same color in row/col
        for (int r = 0; r < size; r++)
        {
            int runLen = 1;
            for (int c = 1; c < size; c++)
            {
                if (matrix[r, c] == matrix[r, c - 1])
                {
                    runLen++;
                    if (runLen == 5) penalty += 3;
                    else if (runLen > 5) penalty += 1;
                }
                else runLen = 1;
            }
        }
        for (int c = 0; c < size; c++)
        {
            int runLen = 1;
            for (int r = 1; r < size; r++)
            {
                if (matrix[r, c] == matrix[r - 1, c])
                {
                    runLen++;
                    if (runLen == 5) penalty += 3;
                    else if (runLen > 5) penalty += 1;
                }
                else runLen = 1;
            }
        }

        // Rule 2: 2x2 blocks of same color
        for (int r = 0; r < size - 1; r++)
        {
            for (int c = 0; c < size - 1; c++)
            {
                bool color = matrix[r, c];
                if (matrix[r, c + 1] == color && matrix[r + 1, c] == color && matrix[r + 1, c + 1] == color)
                {
                    penalty += 3;
                }
            }
        }

        return penalty;
    }
}
