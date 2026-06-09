// [F4-US-005] E2E: panel izquierdo con árbol del sistema de archivos
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
public class FolderTreeE2ETests : PageTest
{
    private const string BaseUrl = "http://localhost:5027";

    [Test]
    public async Task FolderTree_PageLoad_ShowsFilesystemRoots()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();

        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        var directoryItems = Page.Locator(".folder-tree-name");
        var count = await directoryItems.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should show at least one root directory");
    }

    [Test]
    public async Task FolderTree_ExpandRoot_RotatesToggle()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        var expandToggle = Page.Locator(".folder-tree-toggle").First;
        await expandToggle.ClickAsync();

        await Expect(expandToggle).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("expanded"));
    }

    [Test]
    // [F4]
    public async Task FolderTree_ClickFolder_HighlightsSelected()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== CLICKED FOLDER === {folderName}");

        await firstFolder.ClickAsync();

        await Expect(firstFolder).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));
    }

    [Test]
    // [F4]
    public async Task FolderTree_ClickDifferentFolder_SwitchesSelection()
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        var folders = Page.Locator(".folder-tree-name");
        var count = await folders.CountAsync();

        if (count < 2)
        {
            Assert.Pass("Only one folder available; cannot test switching selection.");
            return;
        }

        var firstFolder = folders.First;
        var firstFolderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== FIRST FOLDER === {firstFolderName}");
        await firstFolder.ClickAsync();
        await Expect(firstFolder).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));

        var secondFolder = folders.Nth(1);
        var secondFolderName = await secondFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SECOND FOLDER === {secondFolderName}");
        await secondFolder.ClickAsync();

        await Expect(firstFolder).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));

        await Expect(secondFolder).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));
    }
}
