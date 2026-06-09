// [F5] [F6] Photo HTTP client — calls GET /api/photos, GET /api/media-assets/{id}, and GET /api/media-assets/from-file
using System.Net.Http.Json;
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public class PhotoClient : IPhotoClient
{
    private readonly HttpClient _http;

    public PhotoClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<PhotoGridResponse> GetPhotosAsync(
        string? path = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = $"?path={Uri.EscapeDataString(path ?? "")}&page={page}&pageSize={pageSize}";
        var response = await _http.GetAsync($"/api/photos{query}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PhotoGridResponse>(ct) ?? new PhotoGridResponse();
    }

    public async Task<MediaAssetDetailDto> GetAssetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"/api/media-assets/{id}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MediaAssetDetailDto>(ct)
            ?? throw new InvalidOperationException("Response body was null");
    }

    public async Task<FileMetadataDto?> GetFileMetadataAsync(string filePath, CancellationToken ct = default)
    {
        var encoded = Uri.EscapeDataString(filePath);
        var response = await _http.GetAsync($"/api/media-assets/from-file?path={encoded}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FileMetadataDto>(ct);
    }
}
