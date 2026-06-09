// [F6] File metadata DTO from GET /api/media-assets/from-file
namespace SnapTime.Client.Models;

public class FileMetadataDto
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime? DateTimeOriginal { get; set; }
    public string? SubSecDateTimeOriginal { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public DateTime? FileCreatedAt { get; set; }
    public DateTime? FileModifiedAt { get; set; }
}
