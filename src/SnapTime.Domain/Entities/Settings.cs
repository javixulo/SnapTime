namespace SnapTime.Domain.Entities;

public class Settings
{
    public int Id { get; set; }
    public int ConfidenceThreshold { get; set; } = 80;
    public int MaxConcurrency { get; set; } = 4;
    public int BatchSize { get; set; } = 100;
    public string ImageExtensionsCsv { get; set; } = ".jpg,.jpeg";
    public string VideoExtensionsCsv { get; set; } = ".mp4,.mov,.avi,.mkv,.webm,.m4v";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5-coder:14b";
    public int OllamaTimeoutSeconds { get; set; } = 60;
    public int ThumbnailMaxDimension { get; set; } = 300;
    public int ThumbnailQuality { get; set; } = 80;
}
