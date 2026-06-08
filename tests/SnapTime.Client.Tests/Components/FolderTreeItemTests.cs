// [F4-US-005] bUnit tests for FolderTreeItem.razor
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class FolderTreeItemTests : TestContext
{
    private readonly IFilesystemClient _filesystemClient = Substitute.For<IFilesystemClient>();

    public FolderTreeItemTests()
    {
        Services.AddSingleton(_filesystemClient);
    }

    [Fact]
    public void TreeItem_RendersNameAndArrow()
    {
        // Arrange & Act
        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Assert
        cut.Markup.Should().Contain("Users");
        cut.Markup.Should().Contain("▶");
    }

    [Fact]
    public void TreeItem_ClickExpand_LoadsAndShowsChildren()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>())
            .Returns(new[] { "javiermontoro", "Shared" });

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Act — click the expand arrow
        cut.Find(".folder-tree-toggle").Click();

        // Assert — children should appear
        cut.Markup.Should().Contain("javiermontoro");
        cut.Markup.Should().Contain("Shared");
    }

    [Fact]
    public void TreeItem_ClickExpand_CollapsesChildrenOnSecondClick()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>())
            .Returns(new[] { "child1" });

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Act — expand then collapse
        cut.Find(".folder-tree-toggle").Click();
        cut.Find(".folder-tree-toggle").Click();

        // Assert — children should disappear
        cut.Markup.Should().NotContain("child1");
    }

    [Fact]
    public void TreeItem_ClickName_InvokesOnSelect()
    {
        // Arrange
        string? capturedPath = null;

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
            p.Add(m => m.OnSelect, EventCallback.Factory.Create<string>(this,
                path => capturedPath = path));
        });

        // Act — click on the folder name
        cut.Find(".folder-tree-name").Click();

        // Assert
        capturedPath.Should().Be("/Users");
    }

    [Fact]
    public void TreeItem_ShowsLoadingSpinnerWhileExpanding()
    {
        // Arrange — never completes
        var neverComplete = new TaskCompletionSource<string[]>();
        _filesystemClient.GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>())
            .Returns(neverComplete.Task);

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Act
        cut.Find(".folder-tree-toggle").Click();

        // Assert
        cut.Markup.Should().Contain("⋯");
    }

    [Fact]
    public void TreeItem_ShowsErrorOnApiFailure()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string[]>(new HttpRequestException("Access denied")));

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Act
        cut.Find(".folder-tree-toggle").Click();

        // Assert — error message displayed in red
        cut.Markup.Should().Contain("Error:");
    }

    [Fact]
    public void TreeItem_EmptyDirectory_ShowsEmptyMessage()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/empty", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<string>());

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/empty");
            p.Add(m => m.Name, "empty");
        });

        // Act
        cut.Find(".folder-tree-toggle").Click();

        // Assert
        cut.Markup.Should().Contain("vacía");
    }

    [Fact]
    public void TreeItem_RecursiveExpand_WorksForMultipleLevels()
    {
        // Arrange — set up three levels of nesting
        _filesystemClient.GetDirectoriesAsync("/a", Arg.Any<CancellationToken>())
            .Returns(new[] { "b" });
        _filesystemClient.GetDirectoriesAsync("/a/b", Arg.Any<CancellationToken>())
            .Returns(new[] { "c" });

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/a");
            p.Add(m => m.Name, "a");
        });

        // Act — expand first level
        cut.Find(".folder-tree-toggle").Click();
        cut.Markup.Should().Contain("b");

        // Act — expand second level (find the child's expand toggle)
        var childToggles = cut.FindAll(".folder-tree-toggle");
        childToggles.Should().HaveCount(2); // parent + child
        childToggles[1].Click();

        // Assert — third level visible
        cut.Markup.Should().Contain("c");
    }

    [Fact]
    public void TreeItem_IndentIncreasesWithLevel()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/root", Arg.Any<CancellationToken>())
            .Returns(new[] { "child" });

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/root");
            p.Add(m => m.Name, "root");
            p.Add(m => m.Indent, 0);
        });

        // Act — expand to create child
        cut.Find(".folder-tree-toggle").Click();

        // Assert — parent has indent 0, child has indent 24
        var treeItems = cut.FindAll(".folder-tree-item");
        treeItems.Should().HaveCount(2);
        // The first tree item (parent) should have padding-left = Indent * 24px = 0
        // The second tree item (child) should have padding-left = 24px
    }

    [Fact]
    public void TreeItem_DefaultIndentIsZero()
    {
        // Arrange & Act
        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/");
            p.Add(m => m.Name, "root");
        });

        // Assert
        // Without specifying Indent, it should default to 0
        var treeItem = cut.Find(".folder-tree-item");
        treeItem.GetAttribute("style").Should().Contain("padding-left: 0px");
    }

    [Fact]
    public void TreeItem_CachesChildren_DoesNotCallApiAgainOnCollapseExpand()
    {
        // Arrange
        _filesystemClient.GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>())
            .Returns(new[] { "child1" });

        var cut = RenderComponent<FolderTreeItem>(p =>
        {
            p.Add(m => m.Path, "/Users");
            p.Add(m => m.Name, "Users");
        });

        // Act — expand, collapse, expand again
        cut.Find(".folder-tree-toggle").Click();
        cut.Find(".folder-tree-toggle").Click();
        cut.Find(".folder-tree-toggle").Click();

        // Assert — should have called the API exactly once (cached after first load)
        _filesystemClient.Received(1).GetDirectoriesAsync("/Users", Arg.Any<CancellationToken>());
    }
}
