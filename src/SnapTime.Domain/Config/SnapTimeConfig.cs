// [F0-US-005]
namespace SnapTime.Domain.Config;

public class SnapTimeConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public AnalysisConfig Analysis { get; set; } = new();
    public List<HeuristicConfig> Heuristics { get; set; } = [];
    public OllamaConfig Ollama { get; set; } = new();
    public ThumbnailConfig Thumbnails { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();

    public class DatabaseConfig
    {
        public string Path { get; set; } = "SnapTime.db";
    }

    public class AnalysisConfig
    {
        public int ConfidenceThreshold { get; set; } = 80;
        public int MaxConcurrency { get; set; } = 4;
        public int BatchSize { get; set; } = 100;
        public string[] ImageExtensions { get; set; } = [".jpg", ".jpeg"];
        public string[] VideoExtensions { get; set; } = [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];
    }

    public class OllamaConfig
    {
        public string Endpoint { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "llama3.2";
        public int TimeoutSeconds { get; set; } = 60;
    }

    public class ThumbnailConfig
    {
        public int MaxDimension { get; set; } = 300;
        public int Quality { get; set; } = 80;
    }
}

public class LoggingConfig
{
    public string Level { get; set; } = "Information";
    public string? File { get; set; }
}

public class HeuristicConfig
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public double Weight { get; set; } = 1.0;
}
