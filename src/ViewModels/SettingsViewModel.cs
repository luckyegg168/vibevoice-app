using System.Windows.Input;
using VibeVoice.Infrastructure;
using VibeVoice.Models;
using VibeVoice.Services;

namespace VibeVoice.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GlobalHotkeyManager _hotkeyManager;

    private string _apiEndpoint = string.Empty;
    private int _apiTimeout;
    private string _globalHotkey = string.Empty;
    private bool _minimizeToTray;
    private bool _startMinimized;
    private int _typeDelayMs;
    private string _saveStatusText = string.Empty;

    public string ApiEndpoint
    {
        get => _apiEndpoint;
        set
        {
            SetProperty(ref _apiEndpoint, value);
            OnPropertyChanged(nameof(IsApiEndpointValid));
        }
    }

    public int ApiTimeout { get => _apiTimeout; set => SetProperty(ref _apiTimeout, value); }
    public string GlobalHotkey { get => _globalHotkey; set => SetProperty(ref _globalHotkey, value); }
    public bool MinimizeToTray { get => _minimizeToTray; set => SetProperty(ref _minimizeToTray, value); }
    public bool StartMinimized { get => _startMinimized; set => SetProperty(ref _startMinimized, value); }
    public int TypeDelayMs { get => _typeDelayMs; set => SetProperty(ref _typeDelayMs, value); }
    public string SaveStatusText { get => _saveStatusText; set => SetProperty(ref _saveStatusText, value); }

    public bool IsApiEndpointValid
        => Uri.TryCreate(_apiEndpoint, UriKind.Absolute, out var uri)
           && (uri.Scheme == "http" || uri.Scheme == "https");

    public bool IsHotkeyValid
        => GlobalHotkeyManager.TryParseHotkey(GlobalHotkey, out _, out _);

    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }

    public SettingsViewModel(SettingsService settingsService, GlobalHotkeyManager hotkeyManager)
    {
        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;

        SaveCommand = new RelayCommand(Save, () => IsApiEndpointValid);
        ResetCommand = new RelayCommand(Reset);

        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Current;
        ApiEndpoint = s.ApiEndpoint;
        ApiTimeout = s.ApiTimeoutSeconds;
        GlobalHotkey = s.GlobalHotkey;
        MinimizeToTray = s.MinimizeToTray;
        StartMinimized = s.StartMinimized;
        TypeDelayMs = s.TypeDelayMs;
    }

    private void Save()
    {
        _settingsService.Update(s =>
        {
            s.ApiEndpoint = ApiEndpoint.TrimEnd('/');
            s.ApiTimeoutSeconds = Math.Clamp(ApiTimeout, 1, 120);
            s.GlobalHotkey = GlobalHotkey;
            s.MinimizeToTray = MinimizeToTray;
            s.StartMinimized = StartMinimized;
            s.TypeDelayMs = Math.Clamp(TypeDelayMs, 0, 100);
        });

        // Re-register hotkey
        _hotkeyManager.RegisterHotkey(GlobalHotkey);

        SaveStatusText = "✅ 設定已儲存";
        _ = Task.Delay(3000).ContinueWith(_ =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => SaveStatusText = string.Empty));
    }

    private void Reset()
    {
        var defaults = new AppSettings();
        ApiEndpoint = defaults.ApiEndpoint;
        ApiTimeout = defaults.ApiTimeoutSeconds;
        GlobalHotkey = defaults.GlobalHotkey;
        MinimizeToTray = defaults.MinimizeToTray;
        StartMinimized = defaults.StartMinimized;
        TypeDelayMs = defaults.TypeDelayMs;
    }
}
