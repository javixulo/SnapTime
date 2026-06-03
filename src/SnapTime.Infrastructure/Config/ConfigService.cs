// [F0-US-005]
using System.Text.Json;
using SnapTime.Domain.Config;

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

    public SnapTimeConfig Current => _current;

    // TODO: F2-US-XXX — implement FileSystemWatcher to fire this event
    public event Action<SnapTimeConfig>? OnConfigChanged;

    public ConfigService(string configPath)
    {
        _configPath = configPath;
        _current = Load();
    }

    private SnapTimeConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new SnapTimeConfig();
        }

        var json = File.ReadAllText(_configPath);
        var config = JsonSerializer.Deserialize<SnapTimeConfig>(json, JsonOptions) ?? new SnapTimeConfig();

        if (config.Analysis.ConfidenceThreshold < 0)
            config.Analysis.ConfidenceThreshold = 0;

        foreach (var h in config.Heuristics)
        {
            if (h.Weight < 0)
                h.Weight = 0;
        }

        if (string.IsNullOrEmpty(config.Database.Path))
            config.Database.Path = "SnapTime.db";

        return config;
    }
}
