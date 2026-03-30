using System.IO;
using NAudio.Wave;
using Microsoft.Extensions.Logging;

namespace VibeVoice.Services;

public enum RecordingState
{
    Idle,
    Recording,
    Processing
}

public class AudioRecordingService : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _audioStream;
    private WaveFileWriter? _waveWriter;
    private readonly ILogger<AudioRecordingService> _logger;
    private RecordingState _state = RecordingState.Idle;

    public event EventHandler<RecordingState>? StateChanged;
    public event EventHandler<float>? AudioLevelChanged;

    public RecordingState State
    {
        get => _state;
        private set
        {
            _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public AudioRecordingService(ILogger<AudioRecordingService> logger)
    {
        _logger = logger;
    }

    public bool StartRecording()
    {
        if (State == RecordingState.Recording) return false;

        try
        {
            _audioStream = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, mono - optimal for ASR
                BufferMilliseconds = 50
            };

            _waveWriter = new WaveFileWriter(_audioStream, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            State = RecordingState.Recording;
            _logger.LogInformation("Recording started");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            Cleanup();
            return false;
        }
    }

    public byte[]? StopRecording()
    {
        if (State != RecordingState.Recording) return null;

        try
        {
            _waveIn?.StopRecording();
            State = RecordingState.Processing;
            return GetWavBytes();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording");
            return null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);

        // Calculate audio level for VU meter
        float max = 0;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            float level = Math.Abs(sample / 32768f);
            if (level > max) max = level;
        }
        AudioLevelChanged?.Invoke(this, max);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _logger.LogInformation("Recording device stopped");
    }

    private byte[]? GetWavBytes()
    {
        if (_waveWriter == null || _audioStream == null) return null;

        _waveWriter.Flush();
        var bytes = _audioStream.ToArray();
        Cleanup();
        return bytes;
    }

    private void Cleanup()
    {
        _waveWriter?.Dispose();
        _waveWriter = null;
        _waveIn?.Dispose();
        _waveIn = null;
        _audioStream?.Dispose();
        _audioStream = null;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
