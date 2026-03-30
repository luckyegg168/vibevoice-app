using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VibeVoice.Models;

namespace VibeVoice.Services;

public class HistoryService
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VibeVoice", "history.db");

    private readonly ILogger<HistoryService> _logger;
    private readonly SettingsService _settingsService;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public HistoryService(ILogger<HistoryService> logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
        InitializeDatabase();
        LoadRecent();
    }

    private void InitializeDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS HistoryEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Text TEXT NOT NULL,
                OriginalText TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsSuccess INTEGER NOT NULL DEFAULT 1,
                ErrorMessage TEXT,
                ProcessingTime REAL NOT NULL DEFAULT 0,
                AudioDuration REAL NOT NULL DEFAULT 0,
                ChineseModeUsed INTEGER NOT NULL DEFAULT 0,
                PunctuationModeUsed INTEGER NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    private void LoadRecent()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM HistoryEntries ORDER BY Id DESC LIMIT 100";
            using var reader = cmd.ExecuteReader();
            var entries = new List<HistoryEntry>();
            while (reader.Read())
            {
                entries.Add(ReadEntry(reader));
            }
            foreach (var entry in entries)
                Entries.Add(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history");
        }
    }

    public async Task AddEntryAsync(HistoryEntry entry)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO HistoryEntries
                    (Text, OriginalText, CreatedAt, IsSuccess, ErrorMessage, ProcessingTime, AudioDuration, ChineseModeUsed, PunctuationModeUsed)
                VALUES
                    ($text, $orig, $created, $success, $error, $procTime, $audioDur, $chiMode, $punctMode);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$text", entry.Text);
            cmd.Parameters.AddWithValue("$orig", entry.OriginalText);
            cmd.Parameters.AddWithValue("$created", entry.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$success", entry.IsSuccess ? 1 : 0);
            cmd.Parameters.AddWithValue("$error", (object?)entry.ErrorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$procTime", entry.ProcessingTime);
            cmd.Parameters.AddWithValue("$audioDur", entry.AudioDuration);
            cmd.Parameters.AddWithValue("$chiMode", (int)entry.ChineseModeUsed);
            cmd.Parameters.AddWithValue("$punctMode", (int)entry.PunctuationModeUsed);

            var id = (long)(await cmd.ExecuteScalarAsync())!;
            entry.Id = id;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Entries.Insert(0, entry);
                TrimIfNeeded();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add history entry");
        }
    }

    public async Task DeleteEntryAsync(long id)
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM HistoryEntries WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = Entries.FirstOrDefault(e => e.Id == id);
                if (entry != null) Entries.Remove(entry);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete entry {Id}", id);
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM HistoryEntries";
            await cmd.ExecuteNonQueryAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() => Entries.Clear());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear history");
        }
    }

    private void TrimIfNeeded()
    {
        var max = _settingsService.Current.HistoryMaxCount;
        while (Entries.Count > max)
            Entries.RemoveAt(Entries.Count - 1);

        // Also trim DB
        Task.Run(async () =>
        {
            try
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"""
                    DELETE FROM HistoryEntries
                    WHERE Id NOT IN (
                        SELECT Id FROM HistoryEntries ORDER BY Id DESC LIMIT {max}
                    )
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trim history");
            }
        });
    }

    private static HistoryEntry ReadEntry(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Text = reader.GetString(1),
        OriginalText = reader.GetString(2),
        CreatedAt = DateTime.Parse(reader.GetString(3)),
        IsSuccess = reader.GetInt32(4) == 1,
        ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5),
        ProcessingTime = reader.GetDouble(6),
        AudioDuration = reader.GetDouble(7),
        ChineseModeUsed = (ChineseMode)reader.GetInt32(8),
        PunctuationModeUsed = (PunctuationMode)reader.GetInt32(9)
    };
}
