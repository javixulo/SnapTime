// [F0-US-005] [F10-US-002]
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SnapTime.Domain.Config;
using SnapTime.Domain.Entities;
using SnapTime.Infrastructure.Data;

namespace SnapTime.Infrastructure.Config;

public class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _configPath;
    private SnapTimeConfig _current;
    private bool _initialized;
    private bool _hasLegacySeed;

    public SnapTimeConfig Current => _current;

    public event Action<SnapTimeConfig>? OnConfigChanged;

    public ConfigService(string configPath)
    {
        _configPath = configPath;
        _current = LoadBootstrap();
    }

    public void Initialize(SnapTimeDbContext db)
    {
        if (_initialized) return;

        MigrateLegacyJson();
        LoadRuntimeFromDb(db);

        _initialized = true;
    }

    private SnapTimeConfig LoadBootstrap()
    {
        var config = new SnapTimeConfig();

        if (File.Exists(_configPath))
        {
            var json = File.ReadAllText(_configPath);
            var parsed = JsonSerializer.Deserialize<SnapTimeConfig>(json, JsonOptions);
            if (parsed is not null)
            {
                config.Database = parsed.Database;
                config.Logging = parsed.Logging;
            }
        }

        if (string.IsNullOrEmpty(config.Database.Path))
            config.Database.Path = "SnapTime.db";

        return config;
    }

    private void MigrateLegacyJson()
    {
        if (!File.Exists(_configPath)) return;

        var json = File.ReadAllText(_configPath);

        // Use JsonDocument to check for actual property presence (not value comparison)
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var hasAnalysis = root.TryGetProperty("analysis", out _);
        var hasHeuristics = root.TryGetProperty("heuristics", out _);
        var hasOllama = root.TryGetProperty("ollama", out _);
        var hasThumbnails = root.TryGetProperty("thumbnails", out _);

        if (!hasAnalysis && !hasHeuristics && !hasOllama && !hasThumbnails)
            return;

        var parsed = JsonSerializer.Deserialize<SnapTimeConfig>(json, JsonOptions);
        if (parsed is null) return;

        _hasLegacySeed = true;

        // Persist legacy values in-memory (will be saved to DB when Initialize is called)
        _current.Analysis = parsed.Analysis;
        _current.Heuristics = parsed.Heuristics;
        _current.Ollama = parsed.Ollama;
        _current.Thumbnails = parsed.Thumbnails;
    }

    private void LoadRuntimeFromDb(SnapTimeDbContext db)
    {
        var hadLegacy = _hasLegacySeed;

        var settings = db.Settings.Find(1);
        if (settings is null)
        {
            settings = hadLegacy ? LegacyToSettings() : new Settings();
            db.Settings.Add(settings);
            db.SaveChanges();
            _hasLegacySeed = false;
        }
        else if (_hasLegacySeed)
        {
            // Overwrite DB with legacy values (migration)
            var migrated = LegacyToSettings();
            db.Entry(settings).CurrentValues.SetValues(migrated);
            db.SaveChanges();
            _hasLegacySeed = false;
        }

        _current.Analysis.ConfidenceThreshold = settings.ConfidenceThreshold;
        _current.Analysis.MaxConcurrency = settings.MaxConcurrency;
        _current.Analysis.BatchSize = settings.BatchSize;
        _current.Analysis.ImageExtensions = settings.ImageExtensionsCsv.Split(',', StringSplitOptions.TrimEntries);
        _current.Analysis.VideoExtensions = settings.VideoExtensionsCsv.Split(',', StringSplitOptions.TrimEntries);

        _current.Ollama.Endpoint = settings.OllamaEndpoint;
        _current.Ollama.Model = settings.OllamaModel;
        _current.Ollama.TimeoutSeconds = settings.OllamaTimeoutSeconds;

        _current.Thumbnails.MaxDimension = settings.ThumbnailMaxDimension;
        _current.Thumbnails.Quality = settings.ThumbnailQuality;

        if (!db.HeuristicConfigs.Any())
        {
            db.HeuristicConfigs.AddRange(DefaultHeuristics());
            db.SaveChanges();
        }

        var heuristics = db.HeuristicConfigs.ToList();
        _current.Heuristics = heuristics
            .Select(h => new HeuristicConfig
            {
                Id = h.Id,
                Enabled = h.Enabled,
                Weight = h.Weight,
            })
            .ToList();

        // Only clean JSON after all DB writes succeed (prevents data loss on failure)
        if (hadLegacy)
            CleanBootstrapJson();
    }

    private void CleanBootstrapJson()
    {
        var json = File.ReadAllText(_configPath);
        var parsed = JsonSerializer.Deserialize<SnapTimeConfig>(json, JsonOptions);
        if (parsed is null) return;
        var bootstrap = new { database = parsed.Database, logging = parsed.Logging };
        var cleanJson = JsonSerializer.Serialize(bootstrap, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, cleanJson);
    }

    private Settings LegacyToSettings()
    {
        return new Settings
        {
            Id = 1,
            ConfidenceThreshold = _current.Analysis.ConfidenceThreshold,
            MaxConcurrency = _current.Analysis.MaxConcurrency,
            BatchSize = _current.Analysis.BatchSize,
            ImageExtensionsCsv = string.Join(",", _current.Analysis.ImageExtensions),
            VideoExtensionsCsv = string.Join(",", _current.Analysis.VideoExtensions),
            OllamaEndpoint = _current.Ollama.Endpoint,
            OllamaModel = _current.Ollama.Model,
            OllamaTimeoutSeconds = _current.Ollama.TimeoutSeconds,
            ThumbnailMaxDimension = _current.Thumbnails.MaxDimension,
            ThumbnailQuality = _current.Thumbnails.Quality,
        };
    }

    private static List<HeuristicConfigEntity> DefaultHeuristics()
    {
        return
        [
            new() { Id = "H-001", Enabled = true, Weight = 1.0 },
            new() { Id = "H-002", Enabled = true, Weight = 1.0 },
            new() { Id = "H-003", Enabled = true, Weight = 1.0 },
            new() { Id = "H-004", Enabled = true, Weight = 0.5 },
            new() { Id = "H-005", Enabled = true, Weight = 0.7 },
            new() { Id = "H-006", Enabled = true, Weight = 1.0 },
        ];
    }

    public void UpdateRuntimeFromDb(SnapTimeDbContext db)
    {
        LoadRuntimeFromDb(db);
        OnConfigChanged?.Invoke(_current);
    }
}
