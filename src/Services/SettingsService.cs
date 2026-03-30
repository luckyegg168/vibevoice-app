using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VibeVoice.Models;

namespace VibeVoice.Services;

public class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.json");

    private AppSettings _current;
    private readonly ILogger<SettingsService> _logger;

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Current => _current;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        _current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            _current = settings;
            SettingsChanged?.Invoke(this, settings);
            _logger.LogInformation("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void Update(Action<AppSettings> update)
    {
        var cloned = JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(_current))!;
        update(cloned);
        Save(cloned);
    }
}
