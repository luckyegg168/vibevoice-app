using VibeVoice.Models;

namespace VibeVoice.Services;

/// <summary>
/// Handles punctuation insertion based on selected mode.
/// </summary>
public static class PunctuationService
{
    public static string ApplyPunctuation(string text, PunctuationMode mode)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        text = text.Trim();

        return mode switch
        {
            PunctuationMode.None => text,
            PunctuationMode.Chinese => AddChinesePunctuation(text),
            PunctuationMode.English => AddEnglishPunctuation(text),
            _ => text
        };
    }

    private static string AddChinesePunctuation(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // If text already ends with punctuation, don't add
        if (EndsWithPunctuation(text)) return text;

        return text + "。";
    }

    private static string AddEnglishPunctuation(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (EndsWithPunctuation(text)) return text;

        return text + ".";
    }

    private static bool EndsWithPunctuation(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        char last = text[^1];
        return last is '。' or '，' or '！' or '？' or '；' or '：'
                    or '.' or ',' or '!' or '?' or ';' or ':'
                    or '\n' or '\r';
    }
}
