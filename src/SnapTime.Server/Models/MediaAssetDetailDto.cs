using SnapTime.Domain.Enums;

// [F6] Media asset detail DTO returned by GET /api/media-assets/{id}
namespace SnapTime.Server.Models;

public record MediaAssetDetailDto(
    Guid Id,
    string FilePath,
    string FileName,
    MediaType MediaType,
    long FileSize,
    DateTime? DateTimeOriginal,
    string? SubSecDateTimeOriginal,
    DateTime? CreateDate,
    DateTime? ModifyDate,
    DateTime? FileCreatedAt,
    DateTime? FileModifiedAt,
    int ConfidenceScore,
    DateTime? SuggestedDate,
    string? SuggestedByHeuristic,
    List<EvidenceDto> Evidence
);
