using FluentAssertions;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Tests.Scanner;

public class IDirectoryWalkerTests
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg"];
    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"];

    [Fact]
    public async Task WalkAsync_TenJpgFiles_ReturnsAllFilesAsImageType()
    {
        var walker = new InMemoryDirectoryWalker();
        for (var i = 1; i <= 10; i++)
            walker.AddFile($"/photos/img{i}.jpg");

        var results = await CollectAsync(walker, "/photos", ImageExtensions, VideoExtensions, CancellationToken.None);

        results.Should().HaveCount(10);
        results.Should().AllSatisfy(f => f.Extension.Should().Be(".jpg"));
    }

    [Theory]
    [InlineData(3, 7)]
    [InlineData(5, 5)]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    public async Task WalkAsync_MixedImageAndVideoFiles_ReturnsCorrectCounts(int imageCount, int videoCount)
    {
        var walker = new InMemoryDirectoryWalker();
        for (var i = 1; i <= imageCount; i++)
            walker.AddFile($"/media/photo{i}.jpg");
        for (var i = 1; i <= videoCount; i++)
            walker.AddFile($"/media/video{i}.mp4");

        var results = await CollectAsync(walker, "/media", ImageExtensions, VideoExtensions, CancellationToken.None);

        results.Should().HaveCount(imageCount + videoCount);
        results.Count(f => ImageExtensions.Contains(f.Extension)).Should().Be(imageCount);
        results.Count(f => VideoExtensions.Contains(f.Extension)).Should().Be(videoCount);
    }

    [Fact]
    public async Task WalkAsync_UnsupportedExtensions_FiltersOutNonMediaFiles()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/photo1.jpg");
        walker.AddFile("/photos/photo2.png");
        walker.AddFile("/photos/document.txt");
        walker.AddFile("/photos/photo3.jpeg");
        walker.AddFile("/photos/video.mp4");

        var results = await CollectAsync(walker, "/photos", ImageExtensions, VideoExtensions, CancellationToken.None);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(f => ImageExtensions.Concat(VideoExtensions).Should().Contain(f.Extension));
    }

    [Fact]
    public async Task WalkAsync_NoPermissionSubdirectory_SkipsFilesAndDoesNotThrow()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/root/allowed/photo1.jpg");
        walker.AddFile("/root/allowed/photo2.jpg");
        walker.AddFile("/root/restricted/photo3.jpg");
        walker.AddFile("/root/restricted/sub/photo4.jpg");
        walker.AddForbiddenDirectory("/root/restricted");

        var results = new List<FileInfo>();
        var act = async () =>
        {
            await foreach (var file in walker.WalkAsync("/root", ImageExtensions, VideoExtensions, CancellationToken.None))
                results.Add(file);
        };

        await act.Should().NotThrowAsync();
        results.Should().HaveCount(2);
        results.Should().AllSatisfy(f => f.FullName.Should().Contain("/root/allowed/"));
    }

    [Fact]
    public async Task WalkAsync_NestedSubdirectories_ReturnsAllFilesRecursively()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/root/photo1.jpg");
        walker.AddFile("/root/sub/photo2.jpg");
        walker.AddFile("/root/sub/deep/photo3.jpg");
        walker.AddFile("/root/another/photo4.jpg");

        var results = await CollectAsync(walker, "/root", ImageExtensions, VideoExtensions, CancellationToken.None);

        results.Should().HaveCount(4);
    }

    [Fact]
    public async Task WalkAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var walker = new InMemoryDirectoryWalker();
        for (var i = 1; i <= 10; i++)
            walker.AddFile($"/photos/img{i}.jpg");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () =>
        {
            var results = new List<FileInfo>();
            await foreach (var file in walker.WalkAsync("/photos", ImageExtensions, VideoExtensions, cts.Token))
                results.Add(file);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WalkAsync_NullRootPath_ThrowsArgumentNullException()
    {
        var walker = new InMemoryDirectoryWalker();

        var act = async () =>
        {
            await foreach (var _ in walker.WalkAsync(null!, ImageExtensions, VideoExtensions, CancellationToken.None)) ;
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WalkAsync_NullImageExtensions_ThrowsArgumentNullException()
    {
        var walker = new InMemoryDirectoryWalker();

        var act = async () =>
        {
            await foreach (var _ in walker.WalkAsync("/root", null!, VideoExtensions, CancellationToken.None)) ;
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WalkAsync_NullVideoExtensions_ThrowsArgumentNullException()
    {
        var walker = new InMemoryDirectoryWalker();

        var act = async () =>
        {
            await foreach (var _ in walker.WalkAsync("/root", ImageExtensions, null!, CancellationToken.None)) ;
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task WalkAsync_EmptyRootPath_ThrowsArgumentException()
    {
        var walker = new InMemoryDirectoryWalker();

        var act = async () =>
        {
            await foreach (var _ in walker.WalkAsync("", ImageExtensions, VideoExtensions, CancellationToken.None)) ;
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task WalkAsync_NonExistentRootPath_ReturnsEmpty()
    {
        var walker = new InMemoryDirectoryWalker();
        walker.AddFile("/photos/img1.jpg");
        walker.AddFile("/photos/img2.jpg");

        var results = await CollectAsync(walker, "/nonexistent", ImageExtensions, VideoExtensions, CancellationToken.None);

        results.Should().BeEmpty();
    }

    private static async Task<List<FileInfo>> CollectAsync(
        InMemoryDirectoryWalker walker,
        string rootPath,
        string[] imageExtensions,
        string[] videoExtensions,
        CancellationToken ct)
    {
        var results = new List<FileInfo>();
        await foreach (var file in walker.WalkAsync(rootPath, imageExtensions, videoExtensions, ct))
            results.Add(file);
        return results;
    }
}
