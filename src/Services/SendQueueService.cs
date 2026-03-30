using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace VibeVoice.Services;

/// <summary>
/// Queues text injection requests to prevent race conditions from rapid hotkey presses.
/// Ensures text is injected in order.
/// </summary>
public class SendQueueService : IDisposable
{
    private readonly ConcurrentQueue<string> _queue = new();
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

    public void Enqueue(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _queue.Enqueue(text);
        _logger.LogDebug("Queued text: {Text}", text.Length > 30 ? text[..30] + "..." : text);
        _ = ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        if (!await _semaphore.WaitAsync(0))
            return; // Another task is already processing

        try
        {
            while (_queue.TryDequeue(out var text))
            {
                if (_disposed) return;
                try
                {
                    // Small delay to let the focus window regain foreground
                    await Task.Delay(50);
                    await _injectionService.InjectTextAsync(text);
                    TextSent?.Invoke(this, text);
                    _logger.LogInformation("Text sent: {Chars} chars", text.Length);
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
