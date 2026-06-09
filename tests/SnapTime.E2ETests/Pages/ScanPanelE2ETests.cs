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

    [Test]
    // [F4]
    public async Task ScanPanel_ToggleIncludeSubfolders_TogglesState()
    {
        await Page.GotoAsync("http://localhost:5027");

        await Expect(Page.Locator("text=Sistema de archivos")).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.WaitForAsync(new() { Timeout = 5000 });

        // Select a folder first so the scan panel is active
        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SELECTED FOLDER === {folderName}");
        await firstFolder.ClickAsync();

        var subfoldersToggle = Page.Locator("text=Incluir subcarpetas").First;
        await Expect(subfoldersToggle).ToBeVisibleAsync();
        await TestContext.Out.WriteLineAsync("=== INCLUIR SUBCARPETAS toggle is visible ===");

        // Should be checked by default
        await Expect(subfoldersToggle).ToBeCheckedAsync();
        await TestContext.Out.WriteLineAsync("=== TOGGLE STATE: checked (default) ===");

        // Click → uncheck
        await subfoldersToggle.ClickAsync();
        await Expect(subfoldersToggle).Not.ToBeCheckedAsync();
        await TestContext.Out.WriteLineAsync("=== TOGGLE STATE: unchecked ===");

        // Click again → recheck
        await subfoldersToggle.ClickAsync();
        await Expect(subfoldersToggle).ToBeCheckedAsync();
        await TestContext.Out.WriteLineAsync("=== TOGGLE STATE: checked (after second click) ===");
    }
}
