// [F10-US-003]
namespace SnapTime.Server.Models;

public record HeuristicConfigDto(
    string Id,
    bool? Enabled,
    double? Weight
);

public record ConfigUpdateRequest(
    int? ConfidenceThreshold,
    int? MaxConcurrency,
    int? BatchSize,
    string? ImageExtensionsCsv,
    string? VideoExtensionsCsv,
    string? OllamaEndpoint,
    string? OllamaModel,
    int? OllamaTimeoutSeconds,
    int? ThumbnailMaxDimension,
    int? ThumbnailQuality,
    List<HeuristicConfigDto>? Heuristics
);
