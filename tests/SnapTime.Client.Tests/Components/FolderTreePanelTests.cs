// [F4-US-005] bUnit tests for FolderTreePanel.razor
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class FolderTreePanelTests : TestContext
{
    private readonly IFilesystemClient _filesystemClient = Substitute.For<IFilesystemClient>();

    public FolderTreePanelTests()
    {
        Services.AddSingleton(_filesystemClient);
    }

    [Fact]
    public void Panel_ShowsLoadingOnMount()
    {
        var neverComplete = new TaskCompletionSource<string[]>();
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(neverComplete.Task);

        var cut = RenderComponent<FolderTreePanel>();

        cut.Markup.Should().Contain("Cargando");
    }

    [Fact]
    public void Panel_LoadsAndShowsRootDirectories()
    {
        var rootDirs = new[] { "Users", "Applications" };
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(rootDirs);

        var cut = RenderComponent<FolderTreePanel>();

        cut.Markup.Should().Contain("Users");
        cut.Markup.Should().Contain("Applications");
    }

    [Fact]
    public void Panel_ShowsErrorOnApiFailure()
    {
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string[]>(new HttpRequestException("Connection refused")));

        var cut = RenderComponent<FolderTreePanel>();

        cut.Markup.Should().Contain("Error al cargar raíces");
        cut.Find(".folder-tree-panel-error").Should().NotBeNull();
    }

    [Fact]
    public void Panel_ShowsEmptyMessageWhenNoDirectories()
    {
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        var cut = RenderComponent<FolderTreePanel>();

        cut.Markup.Should().Contain("sin directorios");
    }

    [Fact]
    public void Panel_ClickFolder_HighlightsSelected()
    {
        // Arrange
        var rootDirs = new[] { "Users", "Applications" };
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(rootDirs);

        var cut = RenderComponent<FolderTreePanel>();

        // Act — click on the first folder name
        var folderNames = cut.FindAll(".folder-tree-name");
        folderNames.Should().HaveCount(2);
        folderNames[0].Click();

        // Assert — the clicked folder should get the 'selected' CSS class
        // FAILS: FolderTreeItem doesn't apply 'selected' class based on SelectedPath
        var afterClick = cut.FindAll(".folder-tree-name");
        afterClick[0].ClassName.Should().Contain("selected");
    }

    [Fact]
    public void Panel_ClickSecondFolder_UnselectsFirst()
    {
        // Arrange
        var rootDirs = new[] { "Users", "Applications" };
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(rootDirs);

        var cut = RenderComponent<FolderTreePanel>();

        var folderNames = cut.FindAll(".folder-tree-name");

        // Act — click first folder
        folderNames[0].Click();

        // Assert — first is selected
        // FAILS: no folder gets 'selected' on click
        var afterFirst = cut.FindAll(".folder-tree-name");
        afterFirst[0].ClassName.Should().Contain("selected");

        // Act — click second folder
        afterFirst[1].Click();

        // Assert — first lost selection, second gained it
        var afterSecond = cut.FindAll(".folder-tree-name");
        afterSecond[0].ClassName.Should().NotContain("selected");
        afterSecond[1].ClassName.Should().Contain("selected");
    }

    [Fact]
    public void Panel_SelectedPathIsInitiallyEmpty()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new[] { "Users", "Applications" });

        // Act
        var cut = RenderComponent<FolderTreePanel>();

        // Assert — FolderTreePanel should expose the selected path as a property.
        // Currently FAILS because SelectedPath is not implemented.
        var panelType = typeof(FolderTreePanel);
        var selectedPathProp = panelType.GetProperty("SelectedPath",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        selectedPathProp.Should().NotBeNull();

        // Future — when SelectedPath exists:
        // var selectedPath = selectedPathProp.GetValue(cut.Instance);
        // selectedPath.Should().BeNull();
    }
}
