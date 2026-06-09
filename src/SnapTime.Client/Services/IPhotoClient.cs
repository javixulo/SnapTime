// [F5] Photo HTTP client interface
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public interface IPhotoClient
{
    Task<PhotoGridResponse> GetPhotosAsync(string? path = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<MediaAssetDetailDto> GetAssetDetailAsync(Guid id, CancellationToken ct = default);
    Task<FileMetadataDto?> GetFileMetadataAsync(string filePath, CancellationToken ct = default);
}
