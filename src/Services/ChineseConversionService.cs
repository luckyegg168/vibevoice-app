using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using VibeVoice.Models;

namespace VibeVoice.Services;

/// <summary>
/// Chinese Simplified ↔ Traditional conversion using Windows built-in LCMapString API.
/// Requires no external language packs for most Windows 10/11 systems.
/// </summary>
public class ChineseConversionService
{
    private readonly ILogger<ChineseConversionService> _logger;

    // Windows locale IDs
    private const uint LCID_SIMPLIFIED_CHINESE = 0x0804;
    private const uint LCID_TRADITIONAL_CHINESE = 0x0404;
    private const uint LCMAP_TRADITIONAL_CHINESE = 0x04000000;
    private const uint LCMAP_SIMPLIFIED_CHINESE = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int LCMapString(
        uint locale, uint dwMapFlags, string lpSrcStr, int cchSrc,
        [Out] char[] lpDestStr, int cchDest);

    public ChineseConversionService(ILogger<ChineseConversionService> logger)
    {
        _logger = logger;
    }

    public string Convert(string text, ChineseMode targetMode)
    {
        if (string.IsNullOrEmpty(text)) return text;

        try
        {
            return targetMode == ChineseMode.Traditional
                ? ToTraditional(text)
                : ToSimplified(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chinese conversion failed, returning original text");
            return text;
        }
    }

    private string ToTraditional(string text)
        => LcMapConvert(text, LCID_SIMPLIFIED_CHINESE, LCMAP_TRADITIONAL_CHINESE);

    private string ToSimplified(string text)
        => LcMapConvert(text, LCID_TRADITIONAL_CHINESE, LCMAP_SIMPLIFIED_CHINESE);

    private static string LcMapConvert(string text, uint locale, uint flags)
    {
        if (string.IsNullOrEmpty(text)) return text;

        int bufferSize = LCMapString(locale, flags, text, text.Length, null!, 0);
        if (bufferSize == 0) return text;

        var buffer = new char[bufferSize];
        int result = LCMapString(locale, flags, text, text.Length, buffer, bufferSize);
        return result == 0 ? text : new string(buffer, 0, result);
    }
}
