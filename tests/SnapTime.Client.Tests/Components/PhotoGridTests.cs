// [F5] bUnit tests for PhotoGrid.razor
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class PhotoGridTests : TestContext
{
    private readonly IPhotoClient _photoClient = Substitute.For<IPhotoClient>();

    public PhotoGridTests()
    {
        Services.AddSingleton(_photoClient);
        Services.AddSingleton(new ApiConfig { BaseUrl = "http://localhost:5000" });
    }

    // ──────────────────────────────────────────────
    // Carga y estados
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_showsEmpty_whenNoFolderSelected()
    {
        // [F5] Without a folder selected, show an empty-state message
        var cut = RenderComponent<PhotoGrid>();

        cut.Markup.Should().Contain("Selecciona una carpeta");
    }

    [Fact]
    public void photoGrid_showsLoading_whenFolderSelected()
    {
        // [F5] While the API call is in-flight, show a loading indicator
        var neverComplete = new TaskCompletionSource<PhotoGridResponse>();
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(neverComplete.Task);

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/Users/test"));

        cut.Markup.Should().Contain("Cargando");
    }

    [Fact]
    public void photoGrid_showsError_onApiFailure()
    {
        // [F5] When the API call fails, show an error message
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PhotoGridResponse>(new HttpRequestException("Network error")));

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/Users/test"));

        cut.Markup.Should().Contain("Error");
    }

    [Fact]
    public void photoGrid_loadsAndShowsItems()
    {
        // [F5] Successful load renders thumbnails (image + name)
        var items = new List<PhotoGridItem>
        {
            new() { Name = "photo1.jpg", Path = "/test/photo1.jpg", IsDirectory = false, ThumbnailUrl = "/api/thumbnails/1" },
            new() { Name = "photo2.jpg", Path = "/test/photo2.jpg", IsDirectory = false, ThumbnailUrl = "/api/thumbnails/2" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 2, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/Users/test"));

        cut.Markup.Should().Contain("photo1.jpg");
        cut.Markup.Should().Contain("photo2.jpg");
        cut.FindAll("img").Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────
    // Subcarpetas
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_showsSubfoldersFirst()
    {
        // [F5] Subdirectories appear before regular files in the grid
        var items = new List<PhotoGridItem>
        {
            new() { Name = "photo.jpg",   Path = "/test/photo.jpg",   IsDirectory = false },
            new() { Name = "vacations",   Path = "/test/vacations",   IsDirectory = true },
            new() { Name = "work",        Path = "/test/work",        IsDirectory = true }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 3, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var itemNames = cut.FindAll(".photo-grid-item-name");
        itemNames.Should().HaveCount(3);
        itemNames[0].TextContent.Should().Be("vacations");
        itemNames[1].TextContent.Should().Be("work");
        itemNames[2].TextContent.Should().Be("photo.jpg");
    }

    [Fact]
    public void photoGrid_doubleClickSubfolder_navigatesIn()
    {
        // [F5] Double-clicking a subfolder navigates into it (calls API with subfolder path)
        var callPaths = new List<string?>();
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callPaths.Add(callInfo.ArgAt<string?>(0));
                return new PhotoGridResponse
                {
                    Items = new List<PhotoGridItem>
                    {
                        new() { Name = "subfolder", Path = "/test/subfolder", IsDirectory = true }
                    },
                    TotalCount = 1,
                    Page = 1
                };
            });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));
        callPaths.Clear(); // forget the initial load

        var subfolderItem = cut.Find(".photo-grid-item");
        subfolderItem.DoubleClick();

        callPaths.Should().Contain("/test/subfolder");
    }

    [Fact]
    public void photoGrid_showsFolderIcon_forSubfolders()
    {
        // [F5] Subfolder items display a folder icon
        var items = new List<PhotoGridItem>
        {
            new() { Name = "vacations", Path = "/test/vacations", IsDirectory = true },
            new() { Name = "photo.jpg", Path = "/test/photo.jpg", IsDirectory = false }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 2, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var folderIcons = cut.FindAll(".photo-grid-folder-icon");
        folderIcons.Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────
    // Breadcrumb
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_showsBreadcrumb()
    {
        // [F5] A breadcrumb is visible showing the current folder path
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = [], TotalCount = 0, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/Users/test/photos"));

        var breadcrumb = cut.Find(".photo-grid-breadcrumb");
        breadcrumb.Should().NotBeNull();
        breadcrumb.TextContent.Should().Contain("Users");
        breadcrumb.TextContent.Should().Contain("test");
        breadcrumb.TextContent.Should().Contain("photos");
    }

    [Fact]
    public void photoGrid_clickBreadcrumb_navigatesUp()
    {
        // [F5] Clicking a breadcrumb segment navigates to that parent path
        var callPaths = new List<string?>();
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callPaths.Add(callInfo.ArgAt<string?>(0));
                return new PhotoGridResponse { Items = [], TotalCount = 0, Page = 1 };
            });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/Users/test/photos"));
        callPaths.Clear(); // forget the initial load

        var breadcrumbLinks = cut.FindAll(".photo-grid-breadcrumb-link");
        breadcrumbLinks.Should().NotBeEmpty();

        // Click the "Users" segment (first link)
        breadcrumbLinks[0].Click();

        callPaths.Should().Contain("/Users");
    }

    // ──────────────────────────────────────────────
    // Video badge
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_showsPlayBadge_onVideo()
    {
        // [F5] A video asset shows a play-button badge (▶️) at bottom-right corner
        var items = new List<PhotoGridItem>
        {
            new() { Name = "video.mp4", Path = "/test/video.mp4", IsDirectory = false, MediaType = "Video", ThumbnailUrl = "/api/thumbnails/1" },
            new() { Name = "photo.jpg", Path = "/test/photo.jpg", IsDirectory = false, MediaType = "Image", ThumbnailUrl = "/api/thumbnails/2" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 2, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var playBadges = cut.FindAll(".photo-grid-play-badge");
        playBadges.Should().HaveCount(1);
        playBadges[0].TextContent.Should().Contain("▶");
    }

    // ──────────────────────────────────────────────
    // Status circles
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_statusCircle_gray_whenPending()
    {
        // [F5] Pending status → gray circle
        var items = new List<PhotoGridItem>
        {
            new() { Name = "pending.jpg", Path = "/test/pending.jpg", MediaStatus = "Pending", HasSuggestion = false }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var circle = cut.Find(".photo-grid-status-circle");
        circle.ClassName.Should().Contain("status-pending");
    }

    [Fact]
    public void photoGrid_statusCircle_green_whenCorrect()
    {
        // [F5] Correct date → green circle
        var items = new List<PhotoGridItem>
        {
            new() { Name = "correct.jpg", Path = "/test/correct.jpg", MediaStatus = "Correct" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var circle = cut.Find(".photo-grid-status-circle");
        circle.ClassName.Should().Contain("status-correct");
    }

    [Fact]
    public void photoGrid_statusCircle_red_whenError()
    {
        // [F5] Error status → red circle
        var items = new List<PhotoGridItem>
        {
            new() { Name = "error.jpg", Path = "/test/error.jpg", MediaStatus = "Error" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var circle = cut.Find(".photo-grid-status-circle");
        circle.ClassName.Should().Contain("status-error");
    }

    [Fact]
    public void photoGrid_statusCircle_yellow_whenNoSuggestion()
    {
        // [F5] Scanned but no suggestion → yellow circle
        var items = new List<PhotoGridItem>
        {
            new() { Name = "nosug.jpg", Path = "/test/nosug.jpg", MediaStatus = "NoSuggestion", HasSuggestion = false }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var circle = cut.Find(".photo-grid-status-circle");
        circle.ClassName.Should().Contain("status-no-suggestion");
    }

    [Fact]
    public void photoGrid_statusCircle_blue_whenHasSuggestion()
    {
        // [F5] Has a date suggestion → blue circle
        var items = new List<PhotoGridItem>
        {
            new() { Name = "sug.jpg", Path = "/test/sug.jpg", MediaStatus = "HasSuggestion", HasSuggestion = true }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        var circle = cut.Find(".photo-grid-status-circle");
        circle.ClassName.Should().Contain("status-suggestion");
    }

    // ──────────────────────────────────────────────
    // Tooltip y nombre
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_showsTooltip_withFullFilename()
    {
        // [F5] Hovering over a thumbnail shows the full filename as tooltip
        var longName = "a-very-long-filename-that-should-be-truncated.jpg";
        var items = new List<PhotoGridItem>
        {
            new() { Name = longName, Path = "/test/" + longName, IsDirectory = false, ThumbnailUrl = "/api/thumbnails/1" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(p => p.Add(c => c.SelectedFolderPath, "/test"));

        // The thumbnail container or image should have a title attribute with the full filename
        var thumbnailImg = cut.Find(".photo-grid-thumbnail img");
        thumbnailImg.GetAttribute("title").Should().Be(longName);
    }

    // ──────────────────────────────────────────────
    // Click miniatura
    // ──────────────────────────────────────────────

    [Fact]
    public void photoGrid_clickThumbnail_triggersOnSelect()
    {
        // [F5] Clicking a thumbnail notifies the parent via OnPhotoSelected
        PhotoGridItem? selectedItem = null;
        var items = new List<PhotoGridItem>
        {
            new() { Name = "photo1.jpg", Path = "/test/photo1.jpg", IsDirectory = false, ThumbnailUrl = "/api/thumbnails/1" }
        };
        _photoClient.GetPhotosAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PhotoGridResponse { Items = items, TotalCount = 1, Page = 1 });

        var cut = RenderComponent<PhotoGrid>(parameters => parameters
            .Add(p => p.SelectedFolderPath, "/test")
            .Add(p => p.OnPhotoSelected, (PhotoGridItem item) => { selectedItem = item; }));

        var thumbnail = cut.Find(".photo-grid-thumbnail");
        thumbnail.Click();

        selectedItem.Should().NotBeNull();
        selectedItem!.Name.Should().Be("photo1.jpg");
    }

}
