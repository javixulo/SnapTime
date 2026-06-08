// [F4-US-001] -- E2E: seleccionar carpeta del árbol + escanear
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
public class ScanPanelE2ETests : PageTest
{
    [Test]
    public async Task ScanPanel_SelectFolderAndScan_ShowsProgress()
    {
        await Page.GotoAsync("http://localhost:5027");

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SELECTED FOLDER === {folderName}");

        await firstFolder.ClickAsync();

        var scanButton = Page.Locator("button", new() { HasText = "Escanear" });
        await Expect(scanButton).ToBeVisibleAsync();
        await scanButton.ClickAsync();

        await Expect(Page.Locator("text=Job:")).ToBeVisibleAsync(new() { Timeout = 10000 });

        var body = await Page.TextContentAsync("body");
        await TestContext.Out.WriteLineAsync($"=== BODY ===\n{body}\n=== END ===");

        StringAssert.Contains("Ruta:", body);
    }
}
