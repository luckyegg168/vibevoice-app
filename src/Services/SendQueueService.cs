using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VibeVoice.Services;

/// <summary>
/// Serialises text injection requests so rapid hotkey presses never interleave.
/// </summary>
public class SendQueueService : IDisposable
{
    private readonly record struct QueueItem(string Text, nint TargetWindow);

    private readonly ConcurrentQueue<QueueItem> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TextInjectionService _injectionService;
    private readonly ILogger<SendQueueService> _logger;
    private volatile bool _disposed;

    public event EventHandler<string>? TextSent;
    public event EventHandler<Exception>? SendFailed;

    public SendQueueService(TextInjectionService injectionService, ILogger<SendQueueService> logger)
    {
        _injectionService = injectionService;
        _logger = logger;
    }

    /// <param name="targetWindow">Win32 HWND to focus before injecting. Pass 0 to inject into whatever window is currently active.</param>
    public void Enqueue(string text, nint targetWindow = 0)
    {
        if (string.IsNullOrEmpty(text)) return;
        _queue.Enqueue(new QueueItem(text, targetWindow));
        _logger.LogDebug("Queued {Chars} chars, target=0x{Hwnd:X}", text.Length, targetWindow);
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (!await _semaphore.WaitAsync(0))
            return; // another task is already draining the queue

        try
        {
            while (_queue.TryDequeue(out var item))
            {
                if (_disposed) return;
                try
                {
                    await _injectionService.InjectTextAsync(item.Text, item.TargetWindow);
                    TextSent?.Invoke(this, item.Text);
                    _logger.LogInformation("Sent {Chars} chars", item.Text.Length);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send text");
                    SendFailed?.Invoke(this, ex);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
