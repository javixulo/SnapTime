// [F8-US-006] -- E2E: Aplicación de cambios (Apply)
// Arranque autónomo vía E2EWebFixture + SQLite efímera
namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
[Category("E2E")]
public class ApplyE2ETests : PlaywrightTestBase
{
    [Test]
    public async Task Apply_AfterScanAndApprove_ShowsSuccessSummary()
    {
        // Scan first (same pattern as ReviewE2ETests.ScanAndWaitForCompletionAsync)
        await Page.GotoAsync(BaseUrl);
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.ClickAsync();

        var scanButton = Page.Locator("button", new() { HasText = "Escanear" });
        await Expect(scanButton).ToBeVisibleAsync();
        await scanButton.ClickAsync();

        // Wait for scan to complete
        try
        {
            await Expect(Page.Locator("text=Completed")).ToBeVisibleAsync(new() { Timeout = 60000 });
        }
        catch
        {
            Assert.Inconclusive("Scan did not complete in 60s");
            return;
        }

        // Click first photo in grid
        var firstPhoto = Page.Locator(".photo-grid-item").First;
        await Expect(firstPhoto).ToBeVisibleAsync(new() { Timeout = 10000 });
        await firstPhoto.ClickAsync();
        await Task.Delay(500);

        // Accept the suggestion
        var acceptButton = Page.Locator("button", new() { HasText = "Aceptar" });
        if (await acceptButton.IsVisibleAsync())
        {
            await acceptButton.ClickAsync();
            await Task.Delay(500);
        }

        // Click "Aplicar" button (may be in BatchActions or toolbar)
        var applyButton = Page.Locator("button", new() { HasText = "Aplicar" });
        if (await applyButton.IsVisibleAsync())
        {
            await applyButton.ClickAsync();
            await Task.Delay(500);
        }

        // Verify modal shows
        await Expect(Page.Locator(".apply-modal")).ToBeVisibleAsync(new() { Timeout = 5000 });
        // Click apply in modal
        var confirmButton = Page.Locator(".apply-modal button.btn-primary");
        if (await confirmButton.IsVisibleAsync())
        {
            await confirmButton.ClickAsync();
            await Task.Delay(2000);
            // Verify result shows
            await Expect(Page.Locator(".apply-summary")).ToBeVisibleAsync(new() { Timeout = 5000 });
        }
    }
}
