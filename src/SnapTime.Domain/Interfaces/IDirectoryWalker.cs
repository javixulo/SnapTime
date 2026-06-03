namespace SnapTime.Domain.Interfaces;

/// <summary>
/// Walks a directory tree asynchronously, yielding media files matching the specified extensions.
/// </summary>
public interface IDirectoryWalker
{
    /// <summary>
    /// Enumerates all media files recursively from <paramref name="rootPath"/> that match
    /// the given <paramref name="imageExtensions"/> or <paramref name="videoExtensions"/>.
    /// </summary>
    /// <param name="rootPath">The root directory to start scanning from.</param>
    /// <param name="imageExtensions">File extensions to treat as images (e.g. ".jpg").</param>
    /// <param name="videoExtensions">File extensions to treat as videos (e.g. ".mp4").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of <see cref="FileInfo"/> for matching files.</returns>
    IAsyncEnumerable<FileInfo> WalkAsync(
        string rootPath,
        string[] imageExtensions,
        string[] videoExtensions,
        CancellationToken ct);
}
