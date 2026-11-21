using System.Net;
using System.Net.Sockets;
using EasyFileTransfer.Services;

namespace EasyFileTransfer.Cli;

internal sealed record CliOptions
{
    public int Port { get; init; } = 5050;
    public string? ReceiveDir { get; init; }
    public string? ShareDir { get; init; }
    public string? IpAddress { get; init; }
    public AppLanguage? Language { get; init; }
    public int TimeoutMinutes { get; init; } = 30;
    public bool Once { get; init; }
    public bool NoQr { get; init; }
    public bool QrPlain { get; init; }
    public bool NoColor { get; init; }
    public bool Quiet { get; init; }
    public bool ShowVersion { get; init; }
    public bool ShowHelp { get; init; }
}

internal sealed record CliParseResult(CliOptions? Options, string? ErrorMessage, AppLanguage ErrorLanguage);

internal static class CliOptionsParser
{
    public static CliParseResult Parse(string[] args, AppLanguage fallbackLanguage)
    {
        var errLang = PreScanValidLanguage(args, fallbackLanguage);
        var opts = new CliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrEmpty(arg)) continue;

            if (TrySplitEqualsOption(arg, out var optName, out var inlineVal))
            {
                if (!TryApplyValueOption(optName, inlineVal, ref opts, errLang, out var errMsg))
                {
                    return new CliParseResult(null, errMsg, errLang);
                }
                continue;
            }

            switch (arg)
            {
                case "--port":
                case "-p":
                    if (!TryConsumeNext(args, ref i, arg, out var portVal, errLang, out var portErr))
                        return new CliParseResult(null, portErr, errLang);
                    if (!TryParsePort(portVal, errLang, out var port, out portErr))
                        return new CliParseResult(null, portErr, errLang);
                    opts = opts with { Port = port };
                    break;

                case "--dir":
                case "-d":
                    if (!TryConsumeNext(args, ref i, arg, out var dirVal, errLang, out var dirErr))
                        return new CliParseResult(null, dirErr, errLang);
                    opts = opts with { ReceiveDir = dirVal };
                    break;

                case "--share":
                case "-s":
                    if (!TryConsumeNext(args, ref i, arg, out var shareVal, errLang, out var shareErr))
                        return new CliParseResult(null, shareErr, errLang);
                    opts = opts with { ShareDir = shareVal };
                    break;

                case "--ip":
                    if (!TryConsumeNext(args, ref i, arg, out var ipVal, errLang, out var ipErr))
                        return new CliParseResult(null, ipErr, errLang);
                    if (!TryParseIpv4(ipVal, errLang, out var parsedIp, out ipErr))
                        return new CliParseResult(null, ipErr, errLang);
                    opts = opts with { IpAddress = parsedIp };
                    break;

                case "--lang":
                case "-l":
                    if (!TryConsumeNext(args, ref i, arg, out var langVal, errLang, out var langErr))
                        return new CliParseResult(null, langErr, errLang);
                    if (!CliMessages.TryParseLanguageCode(langVal, out var parsedLang, out _))
                        return new CliParseResult(null, CliMessages.Format(errLang, "ErrInvalidLangFmt", langVal), errLang);
                    opts = opts with { Language = parsedLang };
                    errLang = parsedLang;
                    break;

                case "--timeout":
                case "-t":
                    if (!TryConsumeNext(args, ref i, arg, out var timeoutVal, errLang, out var timeoutErr))
                        return new CliParseResult(null, timeoutErr, errLang);
                    if (!TryParseTimeout(timeoutVal, errLang, out var timeoutMin, out timeoutErr))
                        return new CliParseResult(null, timeoutErr, errLang);
                    opts = opts with { TimeoutMinutes = timeoutMin };
                    break;

                case "--once":
                    opts = opts with { Once = true };
                    break;

                case "--no-qr":
                    opts = opts with { NoQr = true };
                    break;

                case "--qr-plain":
                    opts = opts with { QrPlain = true };
                    break;

                case "--no-color":
                    opts = opts with { NoColor = true };
                    break;

                case "--quiet":
                case "-q":
                    opts = opts with { Quiet = true };
                    break;

                case "--version":
                case "-v":
                    opts = opts with { ShowVersion = true };
                    break;

                case "--help":
                case "-h":
                    opts = opts with { ShowHelp = true };
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        return new CliParseResult(null, CliMessages.Format(errLang, "ErrUnknownOptionFmt", arg), errLang);
                    }

                    var msg = CliMessages.Format(errLang, "ErrUnexpectedArgFmt", arg);
                    if (string.Equals(arg, "send", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "receive", StringComparison.OrdinalIgnoreCase))
                    {
                        msg = $"{msg}{Environment.NewLine}{CliMessages.Get(errLang, "ErrNoSubcommandsHint")}";
                    }
                    return new CliParseResult(null, msg, errLang);
            }
        }

        return new CliParseResult(opts, null, errLang);
    }

    private static AppLanguage PreScanValidLanguage(string[] args, AppLanguage fallback)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if ((arg == "--lang" || arg == "-l") && i + 1 < args.Length)
            {
                if (CliMessages.TryParseLanguageCode(args[i + 1], out var lang, out _))
                {
                    return lang;
                }
            }
            else if (TrySplitEqualsOption(arg, out var name, out var val) && (name == "--lang" || name == "-l"))
            {
                if (CliMessages.TryParseLanguageCode(val, out var lang, out _))
                {
                    return lang;
                }
            }
        }
        return fallback;
    }

    private static bool TrySplitEqualsOption(string arg, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        if (!arg.StartsWith('-')) return false;

        int eqIdx = arg.IndexOf('=');
        if (eqIdx <= 0) return false;

        name = arg[..eqIdx];
        value = arg[(eqIdx + 1)..];
        return name is "--port" or "-p" or "--dir" or "-d" or "--share" or "-s" or "--ip" or "--lang" or "-l" or "--timeout" or "-t";
    }

    private static bool TryApplyValueOption(
        string name,
        string value,
        ref CliOptions opts,
        AppLanguage errLang,
        out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = CliMessages.Format(errLang, "ErrMissingValueFmt", name);
            return false;
        }

        switch (name)
        {
            case "--port":
            case "-p":
                if (!TryParsePort(value, errLang, out var port, out errorMessage)) return false;
                opts = opts with { Port = port };
                return true;

            case "--dir":
            case "-d":
                opts = opts with { ReceiveDir = value };
                return true;

            case "--share":
            case "-s":
                opts = opts with { ShareDir = value };
                return true;

            case "--ip":
                if (!TryParseIpv4(value, errLang, out var ip, out errorMessage)) return false;
                opts = opts with { IpAddress = ip };
                return true;

            case "--lang":
            case "-l":
                if (!CliMessages.TryParseLanguageCode(value, out var lang, out _))
                {
                    errorMessage = CliMessages.Format(errLang, "ErrInvalidLangFmt", value);
                    return false;
                }
                opts = opts with { Language = lang };
                return true;

            case "--timeout":
            case "-t":
                if (!TryParseTimeout(value, errLang, out var min, out errorMessage)) return false;
                opts = opts with { TimeoutMinutes = min };
                return true;

            default:
                errorMessage = CliMessages.Format(errLang, "ErrUnknownOptionFmt", name);
                return false;
        }
    }

    private static bool TryConsumeNext(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        AppLanguage errLang,
        out string? errorMessage)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            errorMessage = CliMessages.Format(errLang, "ErrMissingValueFmt", optionName);
            return false;
        }

        index++;
        value = args[index];
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = CliMessages.Format(errLang, "ErrMissingValueFmt", optionName);
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryParsePort(string raw, AppLanguage errLang, out int port, out string? errorMessage)
    {
        if (int.TryParse(raw.Trim(), out port) && port >= 1 && port <= 65535)
        {
            errorMessage = null;
            return true;
        }
        errorMessage = CliMessages.Format(errLang, "ErrInvalidPortFmt", raw);
        return false;
    }

    private static bool TryParseTimeout(string raw, AppLanguage errLang, out int minutes, out string? errorMessage)
    {
        if (int.TryParse(raw.Trim(), out minutes) && minutes >= 1 && minutes <= 1440)
        {
            errorMessage = null;
            return true;
        }
        errorMessage = CliMessages.Format(errLang, "ErrInvalidTimeoutFmt", raw);
        return false;
    }

    private static bool TryParseIpv4(string raw, AppLanguage errLang, out string normalizedIp, out string? errorMessage)
    {
        var trimmed = raw.Trim();
        if (IPAddress.TryParse(trimmed, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
        {
            normalizedIp = ip.ToString();
            errorMessage = null;
            return true;
        }
        normalizedIp = string.Empty;
        errorMessage = CliMessages.Format(errLang, "ErrInvalidIpFmt", raw);
        return false;
    }
}
