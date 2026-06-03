using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class DirectoryWalker : IDirectoryWalker
{
    private readonly ILogger<DirectoryWalker> _logger;

    public DirectoryWalker(ILogger<DirectoryWalker> logger)
    {
        _logger = logger;
    }

    #pragma warning disable CS1998 // async method lacks await operators; required for yield return in async iterator
    public async IAsyncEnumerable<FileInfo> WalkAsync(
        string rootPath,
        string[] imageExtensions,
        string[] videoExtensions,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(imageExtensions);
        ArgumentNullException.ThrowIfNull(videoExtensions);

        var supportedExtensions = imageExtensions
            .Concat(videoExtensions)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rootDir = new DirectoryInfo(Path.GetFullPath(rootPath));
        if (!rootDir.Exists)
            yield break;

        var directories = new Queue<DirectoryInfo>();
        directories.Enqueue(rootDir);

        while (directories.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            var currentDir = directories.Dequeue();

            try
            {
                foreach (var subDir in currentDir.EnumerateDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    directories.Enqueue(subDir);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Skipping directory {Directory} due to unauthorized access", currentDir.FullName);
                continue;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Skipping directory {Directory} due to IO error", currentDir.FullName);
                continue;
            }

            List<FileInfo>? matchedFiles = null;

            try
            {
                foreach (var file in currentDir.EnumerateFiles())
                {
                    ct.ThrowIfCancellationRequested();

                    if (!supportedExtensions.Contains(file.Extension))
                    {
                        _logger.LogInformation("Skipping file {File} due to unsupported extension {Extension}", file.FullName, file.Extension);
                        continue;
                    }

                    (matchedFiles ??= []).Add(file);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Skipping files in directory {Directory} due to unauthorized access", currentDir.FullName);
                continue;
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Skipping files in directory {Directory} due to IO error", currentDir.FullName);
                continue;
            }

            if (matchedFiles is null)
                continue;

            foreach (var file in matchedFiles)
            {
                ct.ThrowIfCancellationRequested();
                yield return file;
            }
        }
    }
}
