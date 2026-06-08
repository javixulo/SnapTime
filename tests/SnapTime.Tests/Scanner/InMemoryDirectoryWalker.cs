using System.Runtime.CompilerServices;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.Scanner;

public class InMemoryDirectoryWalker : IDirectoryWalker
{
    private readonly List<FileEntry> _files = new();
    private readonly HashSet<string> _forbiddenPaths = new(StringComparer.OrdinalIgnoreCase);

    public record FileEntry(string FullPath);

    public void AddFile(string fullPath)
    {
        _files.Add(new FileEntry(fullPath));
    }

    public void AddForbiddenDirectory(string path)
    {
        _forbiddenPaths.Add(path.TrimEnd('/'));
    }

    #pragma warning disable CS1998 // async method lacks await operators; required for yield return in async iterator
    public async IAsyncEnumerable<FileInfo> WalkAsync(
        string rootPath,
        string[] imageExtensions,
        string[] videoExtensions,
        [EnumeratorCancellation] CancellationToken ct,
        bool includeSubfolders = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(imageExtensions);
        ArgumentNullException.ThrowIfNull(videoExtensions);

        var supported = imageExtensions
            .Concat(videoExtensions)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in _files)
        {
            ct.ThrowIfCancellationRequested();

            if (!file.FullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsForbidden(file.FullPath))
                continue;

            var ext = Path.GetExtension(file.FullPath);
            if (!supported.Contains(ext))
                continue;

            yield return new FileInfo(file.FullPath);
        }
    }

    private bool IsForbidden(string fullPath)
    {
        foreach (var forbidden in _forbiddenPaths)
        {
            if (string.Equals(fullPath, forbidden, StringComparison.OrdinalIgnoreCase))
                return true;
            if (fullPath.StartsWith(forbidden + "/", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
