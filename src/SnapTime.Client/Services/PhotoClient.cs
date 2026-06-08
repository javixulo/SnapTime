// [F5] Photo HTTP client — calls GET /api/photos with pagination
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
}
