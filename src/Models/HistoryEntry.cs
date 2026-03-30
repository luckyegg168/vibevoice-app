namespace VibeVoice.Models;

public class HistoryEntry
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public double ProcessingTime { get; set; }
    public double AudioDuration { get; set; }
    public ChineseMode ChineseModeUsed { get; set; }
    public PunctuationMode PunctuationModeUsed { get; set; }

    public string DisplayText => Text.Length > 80 ? Text[..77] + "..." : Text;
    public string TimeDisplay => CreatedAt.ToString("HH:mm:ss");
    public string DateTimeDisplay => CreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
}
