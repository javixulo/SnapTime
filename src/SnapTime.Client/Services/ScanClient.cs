// [F4-US-000] Scan HTTP client implementation
using System.Net.Http.Json;
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public class ScanClient : IScanClient
{
    private readonly HttpClient _http;

    public ScanClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ScanJobDto?> StartScanAsync(string rootPath, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/jobs", new { rootPath }, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScanJobDto>(cancellationToken: ct);
    }

    public async Task<ScanJobDto?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync<ScanJobDto>($"/api/jobs/{jobId}", ct);
    }
}
