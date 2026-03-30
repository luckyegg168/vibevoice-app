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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TranscriptionService(ILogger<TranscriptionService> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        settingsService.SettingsChanged += (_, _) => { /* no cached client to reset */ };
    }

    // Create a fresh client per request to avoid keep-alive reuse issues with Uvicorn.
    private HttpClient CreateClient()
    {
        var settings = _settingsService.Current;
        var handler = new HttpClientHandler
        {
            // Disable automatic decompression to keep things simple
        };
        var client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(settings.ApiEndpoint),
            Timeout = TimeSpan.FromSeconds(settings.ApiTimeoutSeconds)
        };
        // Tell the server not to keep the connection alive so Uvicorn
        // won't see a stale-connection "Invalid HTTP request" warning.
        client.DefaultRequestHeaders.Connection.Clear();
        client.DefaultRequestHeaders.ConnectionClose = true;
        return client;
    }

    /// <summary>
    /// Sends audio for transcription. Retries up to 3 times on 503 / 429
    /// (model still loading or server busy).
    /// </summary>
    public async Task<(bool IsSuccess, string Text, string? ErrorMessage)> TranscribeAsync(
        byte[] wavBytes, CancellationToken cancellationToken = default)
    {
        if (wavBytes == null || wavBytes.Length == 0)
            return (false, string.Empty, "沒有錄到音訊");

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await TrySendAsync(wavBytes, cancellationToken);

            if (result.IsSuccess)
                return (true, result.Text, null);

            if (result.ShouldRetry && attempt < maxAttempts)
            {
                int delayMs = attempt * 2000;   // 2 s then 4 s
                _logger.LogWarning("Attempt {A}/{Max}: {Err}  — retrying in {D} ms",
                    attempt, maxAttempts, result.ErrorMessage, delayMs);
                await Task.Delay(delayMs, cancellationToken);
                continue;
            }

            return (false, string.Empty, result.ErrorMessage);
        }

        return (false, string.Empty, "重試多次後仍然失敗");
    }

    private async Task<(bool IsSuccess, bool ShouldRetry, string Text, string? ErrorMessage)> TrySendAsync(
        byte[] wavBytes, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var url = $"{settings.ApiEndpoint.TrimEnd('/')}/api/v1/transcribe?return_format=transcription_only";

        using var client = CreateClient();
        try
        {
            using var form = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(wavBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            form.Add(audioContent, "file", "recording.wav");

            _logger.LogInformation("POST {Url} ({Bytes} bytes)", url, wavBytes.Length);
            var response = await client.PostAsync(url, form, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Response {Code}: {Body}", (int)response.StatusCode, body);

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                bool retry = code is 503 or 429;

                string? detail = null;
                try
                {
                    var errJson = JsonSerializer.Deserialize<JsonObject>(body, JsonOptions);
                    detail = errJson?["detail"]?.GetValue<string>()
                          ?? errJson?["message"]?.GetValue<string>()
                          ?? errJson?["error"]?.GetValue<string>();
                }
                catch { /* body is not JSON */ }

                string msg = code switch
                {
                    503 => $"服務暫時不可用（{detail ?? "模型可能仍在載入，請稍候再試"}）",
                    429 => "伺服器忙碌中，請稍後再試",
                    400 => $"音訊格式錯誤：{detail ?? response.ReasonPhrase}",
                    _   => $"API 回傳 {code}: {detail ?? response.ReasonPhrase}"
                };
                _logger.LogWarning("HTTP {Code} from {Url}: {Body}", code, url, body);
                return (false, retry, string.Empty, msg);
            }

            var text = ParseTranscriptionResponse(body);
            if (text is null)
                return (false, false, string.Empty, "無法解析 API 回應");

            if (string.IsNullOrWhiteSpace(text))
                return (false, false, string.Empty, "未偵測到語音內容");

            return (true, false, text.Trim(), null);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Request to {Url} timed out", url);
            return (false, false, string.Empty, $"連線逾時（{settings.ApiTimeoutSeconds} 秒）");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error for {Url}", url);
            return (false, false, string.Empty, $"無法連線至 API：{ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error for {Url}", url);
            return (false, false, string.Empty, $"發生錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// Handles two response shapes:
    ///   1. Plain string  (return_format=transcription_only)
    ///   2. JSON object   { "status": "success", "transcription": "..." }
    /// </summary>
    private static string? ParseTranscriptionResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        // Plain string response
        if (!body.TrimStart().StartsWith('{') && !body.TrimStart().StartsWith('['))
            return body.Trim('"', '\n', '\r', ' ');

        try
        {
            var json = JsonSerializer.Deserialize<JsonObject>(body, JsonOptions);
            if (json == null) return null;

            // { "status": "success", "transcription": "..." }
            var status = json["status"]?.GetValue<string>();
            if (status == "success")
                return json["transcription"]?.GetValue<string>();

            // { "text": "..." }  (whisper-asr-webservice style)
            if (json.ContainsKey("text"))
                return json["text"]?.GetValue<string>();

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <returns>(modelReady, statusMessage)</returns>
    public async Task<(bool Online, string Status)> CheckHealthAsync()
    {
        var baseUrl = _settingsService.Current.ApiEndpoint.TrimEnd('/');
        try
        {
            using var client = CreateClient();
            client.Timeout = TimeSpan.FromSeconds(4);
            var response = await client.GetAsync($"{baseUrl}/health");
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                return (false, code == 503 ? "⚠️ 服務啟動中，請稍候…" : $"❌ 回傳 {code}");
            }

            // Parse model_loaded from HealthResponse
            try
            {
                var json = JsonSerializer.Deserialize<JsonObject>(body, JsonOptions);
                bool modelLoaded = json?["model_loaded"]?.GetValue<bool>() ?? false;
                string? modelId = json?["model_id"]?.GetValue<string>();
                string? device = json?["device"] is JsonObject d
                    ? d["type"]?.GetValue<string>() : null;

                if (!modelLoaded)
                    return (false, "⚠️ 模型載入中，請稍候…");

                string detail = string.IsNullOrEmpty(modelId) ? string.Empty : $" ({modelId}";
                if (!string.IsNullOrEmpty(device)) detail += $"/{device}";
                if (!string.IsNullOrEmpty(detail)) detail += ")";
                return (true, $"✅ 就緒{detail}");
            }
            catch
            {
                return (true, "✅ 連線正常");
            }
        }
        catch (HttpRequestException)
        {
            return (false, "❌ 無法連線至 API");
        }
        catch
        {
            return (false, "❌ 連線逾時");
        }
    }
}
