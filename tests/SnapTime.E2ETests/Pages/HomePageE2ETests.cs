// [F0-US-010] — Smoketest Playwright: navega a la página principal y verifica el título
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
public class HomePageE2ETests : PageTest
{
    [Test]
    public async Task HomePage_LoadsAndContainsSnapTimeInTitle()
    {
        // Act
        await Page.GotoAsync("http://localhost:5027");

        // Assert
        StringAssert.Contains("SnapTime", await Page.TitleAsync());
    }
}
