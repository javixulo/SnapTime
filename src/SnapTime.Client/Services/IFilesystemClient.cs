// [F4-US-005] Filesystem HTTP client interface
namespace SnapTime.Client.Services;

public interface IFilesystemClient
{
    Task<string[]> GetDirectoriesAsync(string? path = null, CancellationToken ct = default);
}
