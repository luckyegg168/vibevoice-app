using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VibeVoice.Models;

namespace VibeVoice.Services;

public class TranscriptionService
{
    private readonly ILogger<TranscriptionService> _logger;
    private readonly SettingsService _settingsService;
    private HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TranscriptionService(ILogger<TranscriptionService> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _httpClient = CreateHttpClient();
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private HttpClient CreateHttpClient()
    {
        var settings = _settingsService.Current;
        var client = new HttpClient
        {
            BaseAddress = new Uri(settings.ApiEndpoint),
            Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds)
        };
        return client;
    }

    private void OnSettingsChanged(object? sender, AppSettings e)
    {
        _httpClient?.Dispose();
        _httpClient = CreateHttpClient();
    }

    public async Task<(bool IsSuccess, string Text, string? ErrorMessage)> TranscribeAsync(
        byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        if (wavBytes == null || wavBytes.Length == 0)
            return (false, string.Empty, "No audio data captured.");

        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(wavBytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            content.Add(fileContent, "file", "recording.wav");

            var requestUrl = $"{_httpClient.BaseAddress!.AbsoluteUri.TrimEnd('/')}/api/v1/transcribe?return_format=transcription_only";

            _logger.LogInformation("Sending {Bytes} bytes to {Url}", wavBytes.Length, requestUrl);

            var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("API response: {Body}", responseBody);

            if (!response.IsSuccessStatusCode)
            {
                var errMsg = $"API returned {(int)response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogWarning(errMsg);
                return (false, string.Empty, errMsg);
            }

            var transcriptionResponse = JsonSerializer.Deserialize<JsonObject>(responseBody, JsonOptions);
            if (transcriptionResponse == null)
                return (false, string.Empty, "Empty response from API.");

            var status = transcriptionResponse["status"]?.GetValue<string>();
            if (status != "success")
            {
                var errMsg = transcriptionResponse["error"]?.GetValue<string>() ?? "Unknown API error.";
                return (false, string.Empty, errMsg);
            }

            // Handle transcription field (transcription_only returns string directly)
            var transcriptionNode = transcriptionResponse["transcription"];
            var text = transcriptionNode?.GetValue<string>() ?? string.Empty;

            return (true, text.Trim(), null);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Transcription timed out");
            return (false, string.Empty, $"連線逾時（{_settingsService.Current.ApiTimeoutSeconds} 秒）");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            return (false, string.Empty, $"無法連線至 API: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during transcription");
            return (false, string.Empty, $"發生錯誤: {ex.Message}");
        }
    }

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var url = $"{_httpClient.BaseAddress!.AbsoluteUri.TrimEnd('/')}/health";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync(url, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
