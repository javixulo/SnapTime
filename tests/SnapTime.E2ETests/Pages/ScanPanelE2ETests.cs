// [F4-US-000] -- E2E: escanea sample/ y verifica que el scan arranca
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
public class ScanPanelE2ETests : PageTest
{
    [Test]
    public async Task ScanPanel_WithSamplePath_StartsScanAndShowsProgress()
    {
        await Page.GotoAsync("http://localhost:5027");

        await Page.Locator("input").FillAsync("/Users/javiermontoro/Projects/SnapTime/sample");

        await Page.Locator("button", new() { HasText = "Escanear" }).ClickAsync();

        await Task.Delay(3000);

        var body = await Page.TextContentAsync("body");
        await TestContext.Out.WriteLineAsync($"=== BODY ===\n{body}\n=== END ===");

        StringAssert.Contains("Running", body);
    }
}
