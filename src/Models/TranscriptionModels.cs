using System.Text.Json.Serialization;

namespace VibeVoice.Models;

public class TranscriptionRequest
{
    public string FilePath { get; set; } = string.Empty;
    public string ReturnFormat { get; set; } = "transcription_only";
    public string? Language { get; set; }
}

public class TranscriptionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("transcription")]
    public object? Transcription { get; set; }

    [JsonPropertyName("processing_time")]
    public double ProcessingTime { get; set; }

    [JsonPropertyName("audio_duration")]
    public double AudioDuration { get; set; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("model_loaded")]
    public bool ModelLoaded { get; set; }
}

public class ModelInfo
{
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;
}
