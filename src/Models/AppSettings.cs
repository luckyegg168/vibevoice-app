namespace VibeVoice.Models;

public class AppSettings
{
    public string ApiEndpoint { get; set; } = "http://192.168.80.60:8000";
    public int ApiTimeoutSeconds { get; set; } = 10;
    public ChineseMode ChineseMode { get; set; } = ChineseMode.Traditional;
    public PunctuationMode PunctuationMode { get; set; } = PunctuationMode.Chinese;
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+V";
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public int HistoryMaxCount { get; set; } = 1000;
    public int TypeDelayMs { get; set; } = 2;
}

public enum ChineseMode
{
    Traditional,
    Simplified
}

public enum PunctuationMode
{
    None,
    Chinese,
    English
}
