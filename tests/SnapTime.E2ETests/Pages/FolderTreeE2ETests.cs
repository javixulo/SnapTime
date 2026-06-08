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
}
