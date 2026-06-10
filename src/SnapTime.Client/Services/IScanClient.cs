// [F4-US-000] Scan HTTP client interface
using SnapTime.Client.Models;

namespace SnapTime.Client.Services;

public interface IScanClient
{
    Task<ScanJobDto?> StartScanAsync(string rootPath, bool includeSubfolders = true, CancellationToken ct = default);

    Task<ScanJobDto?> GetJobAsync(Guid jobId, CancellationToken ct = default);

    Task CancelScanAsync(Guid jobId, CancellationToken ct = default);
}
