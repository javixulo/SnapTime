// [F4-US-005] Filesystem HTTP client implementation
using System.Net.Http.Json;

namespace SnapTime.Client.Services;

public class FilesystemClient : IFilesystemClient
{
    private readonly HttpClient _http;

    public FilesystemClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string[]> GetDirectoriesAsync(string? path = null, CancellationToken ct = default)
    {
        var url = "/api/filesystem/directories";

        if (path is not null)
        {
            var encodedPath = Uri.EscapeDataString(path);
            url = $"{url}?path={encodedPath}";
        }

        return await _http.GetFromJsonAsync<string[]>(url, ct) ?? [];
    }
}
