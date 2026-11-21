using System.Text;

namespace EasyFileTransfer.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        try { Console.InputEncoding = Encoding.UTF8; } catch { }

        return await CliApp.RunAsync(args);
    }
}
