// [F4-US-005] — Smoketest bUnit: renderiza Home.razor y verifica el layout
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Pages;
using SnapTime.Client.Services;
using SnapTime.Client.Models;

namespace SnapTime.Client.Tests.Pages;

public class HomePageTests : TestContext
{
    private readonly IFilesystemClient _filesystemClient = Substitute.For<IFilesystemClient>();
    private readonly IScanClient _scanClient = Substitute.For<IScanClient>();
    private readonly IPhotoClient _photoClient = Substitute.For<IPhotoClient>();
    private readonly IScanStateService _scanStateService = Substitute.For<IScanStateService>();

    public HomePageTests()
    {
        Services.AddSingleton(_filesystemClient);
        Services.AddSingleton(_scanClient);
        Services.AddSingleton(_photoClient);
        Services.AddSingleton(_scanStateService);
        Services.AddSingleton(new ApiConfig { BaseUrl = "http://localhost:3000" });
    }

    [Fact]
    public void Home_RendersSnapTimeLayout()
    {
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new[] { "Users" });

        var cut = RenderComponent<Home>();

        cut.Markup.Should().Contain("snaptime-layout");
        cut.Markup.Should().Contain("snaptime-left-panel");
        cut.Markup.Should().Contain("snaptime-center-panel");
        cut.Markup.Should().Contain("snaptime-right-panel");
        cut.Markup.Should().Contain("Sistema de archivos");
        cut.Markup.Should().Contain("Users");
    }

    [Fact]
    public void homePage_passesSelectedAssetPath_toPhotoDetail()
    {
        // [F6-BUG1] After clicking a photo in the grid, PhotoDetail should receive SelectedAssetPath
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new[] { "Users" });

        var photoItems = new List<PhotoGridItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "vacation.jpg",
                Path = "/Users/vacation.jpg",
                IsDirectory = false,
                ThumbnailUrl = "/api/thumbnails/1"
            }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = photoItems, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<Home>();

        // Select a folder in the tree so the grid loads
        var folderName = cut.Find(".folder-tree-name");
        folderName.Click();

        // The grid should now have items — click a photo thumbnail
        var thumbnail = cut.Find(".photo-grid-thumbnail");
        thumbnail.Click();

        // Assert: PhotoDetail should be rendered with SelectedAssetPath
        var photoDetail = cut.FindComponent<PhotoDetail>();
        photoDetail.Instance.SelectedAssetPath.Should().Be("/Users/vacation.jpg");
    }
}
