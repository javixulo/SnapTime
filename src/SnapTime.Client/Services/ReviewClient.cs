// [F7-US-004] Review HTTP client implementation — POST /api/reviews/single, POST /api/reviews/batch
using System.Net.Http.Json;
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public class ReviewClient : IReviewClient
{
    private readonly HttpClient _http;

    public ReviewClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<MediaAssetDto> SingleReviewAsync(Guid assetId, string status, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/reviews/single", new { assetId, status }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MediaAssetDto>(ct)
            ?? throw new InvalidOperationException("Response body was null");
    }

    public async Task<List<Guid>> BatchReviewAsync(string scope, string status, string? rootPath = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/reviews/batch", new { scope, status, rootPath }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Guid>>(ct) ?? [];
    }
}
