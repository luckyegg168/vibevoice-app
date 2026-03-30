using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NHotkey;
using NHotkey.Wpf;

namespace VibeVoice.Infrastructure;

/// <summary>
/// Manages global hotkey registration using NHotkey.
/// </summary>
public class GlobalHotkeyManager : IDisposable
{
    private readonly ILogger<GlobalHotkeyManager> _logger;
    private string? _currentHotkey;
    private const string RecordingHotkeyName = "VibeVoiceRecord";

    public event EventHandler? RecordingHotkeyPressed;

    public GlobalHotkeyManager(ILogger<GlobalHotkeyManager> logger)
    {
        _logger = logger;
    }

    public bool RegisterHotkey(string hotkeyString)
    {
        try
        {
            Unregister();

            if (!TryParseHotkey(hotkeyString, out var key, out var modifiers))
            {
                _logger.LogWarning("Invalid hotkey: {Hotkey}", hotkeyString);
                return false;
            }

            HotkeyManager.Current.AddOrReplace(RecordingHotkeyName, key, modifiers, OnHotkeyPressed);
            _currentHotkey = hotkeyString;
            _logger.LogInformation("Hotkey registered: {Hotkey}", hotkeyString);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register hotkey: {Hotkey}", hotkeyString);
            return false;
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        e.Handled = true;
        RecordingHotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    public void Unregister()
    {
        try
        {
            HotkeyManager.Current.Remove(RecordingHotkeyName);
        }
        catch { }
        _currentHotkey = null;
    }

    public static bool TryParseHotkey(string hotkeyString, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;

        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" or "windows" => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
        }

        if (!Enum.TryParse(parts[^1], true, out key))
            return false;

        return key != Key.None;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }
}
