// [F7-US-004] Review HTTP client interface — aprobar/rechazar sugerencias
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public interface IReviewClient
{
    Task<MediaAssetDto> SingleReviewAsync(Guid assetId, string status, CancellationToken ct = default);
    Task<List<Guid>> BatchReviewAsync(string scope, string status, string? rootPath = null, CancellationToken ct = default);
}
