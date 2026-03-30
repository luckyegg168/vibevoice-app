using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VibeVoice.Infrastructure;
using VibeVoice.Models;
using VibeVoice.Services;

namespace VibeVoice.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly AudioRecordingService _recordingService;
    private readonly TranscriptionService _transcriptionService;
    private readonly ChineseConversionService _conversionService;
    private readonly SendQueueService _sendQueueService;
    private readonly HistoryService _historyService;
    private readonly SettingsService _settingsService;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private readonly ILogger<MainViewModel> _logger;

    private string _statusText = "就緒";
    private string _previewText = string.Empty;
    private bool _isRecording;
    private bool _isProcessing;
    private float _audioLevel;
    private string _apiStatusText = "未檢查";
    private bool _isApiOnline;
    private ChineseMode _chineseMode;
    private PunctuationMode _punctuationMode;

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }
    public bool IsRecording { get => _isRecording; set => SetProperty(ref _isRecording, value); }
    public bool IsProcessing { get => _isProcessing; set => SetProperty(ref _isProcessing, value); }
    public float AudioLevel { get => _audioLevel; set => SetProperty(ref _audioLevel, value); }
    public string ApiStatusText { get => _apiStatusText; set => SetProperty(ref _apiStatusText, value); }
    public bool IsApiOnline { get => _isApiOnline; set => SetProperty(ref _isApiOnline, value); }

    public ChineseMode ChineseMode
    {
        get => _chineseMode;
        set
        {
            SetProperty(ref _chineseMode, value);
            _settingsService.Update(s => s.ChineseMode = value);
            OnPropertyChanged(nameof(IsTraditional));
            OnPropertyChanged(nameof(IsSimplified));
        }
    }

    public PunctuationMode PunctuationMode
    {
        get => _punctuationMode;
        set
        {
            SetProperty(ref _punctuationMode, value);
            _settingsService.Update(s => s.PunctuationMode = value);
            OnPropertyChanged(nameof(PunctuationNone));
            OnPropertyChanged(nameof(PunctuationChinese));
            OnPropertyChanged(nameof(PunctuationEnglish));
        }
    }

    public bool IsTraditional { get => ChineseMode == ChineseMode.Traditional; set { if (value) ChineseMode = ChineseMode.Traditional; } }
    public bool IsSimplified { get => ChineseMode == ChineseMode.Simplified; set { if (value) ChineseMode = ChineseMode.Simplified; } }
    public bool PunctuationNone { get => PunctuationMode == PunctuationMode.None; set { if (value) PunctuationMode = PunctuationMode.None; } }
    public bool PunctuationChinese { get => PunctuationMode == PunctuationMode.Chinese; set { if (value) PunctuationMode = PunctuationMode.Chinese; } }
    public bool PunctuationEnglish { get => PunctuationMode == PunctuationMode.English; set { if (value) PunctuationMode = PunctuationMode.English; } }

    public System.Collections.ObjectModel.ObservableCollection<HistoryEntry> HistoryEntries
        => _historyService.Entries;

    public ICommand ToggleRecordingCommand { get; }
    public ICommand CheckApiStatusCommand { get; }
    public ICommand ResendEntryCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand CopyEntryCommand { get; }

    public MainViewModel(
        AudioRecordingService recordingService,
        TranscriptionService transcriptionService,
        ChineseConversionService conversionService,
        SendQueueService sendQueueService,
        HistoryService historyService,
        SettingsService settingsService,
        GlobalHotkeyManager hotkeyManager,
        ILogger<MainViewModel> logger)
    {
        _recordingService = recordingService;
        _transcriptionService = transcriptionService;
        _conversionService = conversionService;
        _sendQueueService = sendQueueService;
        _historyService = historyService;
        _settingsService = settingsService;
        _hotkeyManager = hotkeyManager;
        _logger = logger;

        // Init from settings
        _chineseMode = settingsService.Current.ChineseMode;
        _punctuationMode = settingsService.Current.PunctuationMode;

        // Commands
        ToggleRecordingCommand = new RelayCommand(ToggleRecording, () => !IsProcessing);
        CheckApiStatusCommand = new AsyncRelayCommand(CheckApiStatusAsync);
        ResendEntryCommand = new RelayCommand(ResendEntry);
        DeleteEntryCommand = new RelayCommand(async o =>
        {
            if (o is HistoryEntry e) await DeleteEntryAsync(e);
        });
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        CopyEntryCommand = new RelayCommand(CopyEntry);

        // Wire up audio events
        _recordingService.StateChanged += OnRecordingStateChanged;
        _recordingService.AudioLevelChanged += OnAudioLevelChanged;

        // Hotkey
        _hotkeyManager.RecordingHotkeyPressed += OnHotkeyPressed;
        RegisterHotkey();

        // Settings changes
        _settingsService.SettingsChanged += OnSettingsChanged;

        // Initial API check
        _ = CheckApiStatusAsync();
    }

    private void RegisterHotkey()
    {
        var hotkey = _settingsService.Current.GlobalHotkey;
        if (!_hotkeyManager.RegisterHotkey(hotkey))
            StatusText = $"快捷鍵 {hotkey} 註冊失敗";
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(ToggleRecording);
    }

    private void ToggleRecording()
    {
        if (IsProcessing) return;
        if (IsRecording)
            StopRecordingAndTranscribe();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (_recordingService.StartRecording())
        {
            IsRecording = true;
            StatusText = "🎤 錄音中…";
            PreviewText = string.Empty;
        }
        else
        {
            StatusText = "❌ 無法啟動麥克風";
        }
    }

    private void StopRecordingAndTranscribe()
    {
        IsRecording = false;
        IsProcessing = true;
        StatusText = "⏳ 辨識中…";

        var wavBytes = _recordingService.StopRecording();
        if (wavBytes == null || wavBytes.Length < 100)
        {
            IsProcessing = false;
            StatusText = "就緒 (無有效錄音)";
            return;
        }

        _ = TranscribeAsync(wavBytes);
    }

    private async Task TranscribeAsync(byte[] wavBytes)
    {
        try
        {
            var (success, rawText, errMsg) = await _transcriptionService.TranscribeAsync(wavBytes);

            if (!success)
            {
                await AddFailureEntry(errMsg ?? "Unknown error");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsProcessing = false;
                    StatusText = $"❌ {errMsg}";
                });
                return;
            }

            // Apply Chinese conversion
            var convertedText = _conversionService.Convert(rawText, ChineseMode);

            // Apply punctuation
            var finalText = PunctuationService.ApplyPunctuation(convertedText, PunctuationMode);

            // Update UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                PreviewText = finalText;
                IsProcessing = false;
                StatusText = "✅ 辨識完成";
            });

            // Save to history
            var entry = new HistoryEntry
            {
                Text = finalText,
                OriginalText = rawText,
                IsSuccess = true,
                ChineseModeUsed = ChineseMode,
                PunctuationModeUsed = PunctuationMode
            };
            await _historyService.AddEntryAsync(entry);

            // Inject text
            _sendQueueService.Enqueue(finalText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription failed");
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsProcessing = false;
                StatusText = $"❌ 發生錯誤: {ex.Message}";
            });
        }
    }

    private async Task AddFailureEntry(string errorMessage)
    {
        var entry = new HistoryEntry
        {
            Text = string.Empty,
            OriginalText = string.Empty,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            ChineseModeUsed = ChineseMode,
            PunctuationModeUsed = PunctuationMode
        };
        await _historyService.AddEntryAsync(entry);
    }

    private void OnRecordingStateChanged(object? sender, RecordingState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (state == RecordingState.Idle)
                AudioLevel = 0;
        });
    }

    private void OnAudioLevelChanged(object? sender, float level)
    {
        Application.Current.Dispatcher.Invoke(() => AudioLevel = level);
    }

    private async Task CheckApiStatusAsync()
    {
        ApiStatusText = "檢查中…";
        var online = await _transcriptionService.CheckHealthAsync();
        IsApiOnline = online;
        ApiStatusText = online ? "✅ 連線正常" : "❌ 無法連線";
    }

    private void ResendEntry(object? parameter)
    {
        if (parameter is HistoryEntry entry && entry.IsSuccess && !string.IsNullOrEmpty(entry.Text))
        {
            _sendQueueService.Enqueue(entry.Text);
            StatusText = $"重發: {entry.DisplayText}";
        }
    }

    public async Task DeleteEntryAsync(HistoryEntry entry)
    {
        await _historyService.DeleteEntryAsync(entry.Id);
    }

    private async Task ClearHistoryAsync()
    {
        await _historyService.ClearAllAsync();
        StatusText = "已清除歷史紀錄";
    }

    private void CopyEntry(object? parameter)
    {
        if (parameter is HistoryEntry entry && !string.IsNullOrEmpty(entry.Text))
        {
            Clipboard.SetText(entry.Text);
            StatusText = "已複製至剪貼簿";
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _chineseMode = settings.ChineseMode;
        _punctuationMode = settings.PunctuationMode;
        OnPropertyChanged(nameof(IsTraditional));
        OnPropertyChanged(nameof(IsSimplified));
        OnPropertyChanged(nameof(PunctuationNone));
        OnPropertyChanged(nameof(PunctuationChinese));
        OnPropertyChanged(nameof(PunctuationEnglish));
        RegisterHotkey();
    }
}
