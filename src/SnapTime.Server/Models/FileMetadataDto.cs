// [F6] File metadata DTO returned by GET /api/media-assets/from-file
namespace SnapTime.Server.Models;

public record FileMetadataDto(
    string FilePath,
    string FileName,
    long FileSize,
    DateTime? DateTimeOriginal,
    string? SubSecDateTimeOriginal,
    DateTime? CreateDate,
    DateTime? ModifyDate,
    DateTime? FileCreatedAt,
    DateTime? FileModifiedAt
);
