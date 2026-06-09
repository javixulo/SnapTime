// [F0-US-010] — Smoketest Playwright: navega a la página principal y verifica el título
namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
[Category("E2E")]
public class HomePageE2ETests : PlaywrightTestBase
{
    [Test]
    public async Task HomePage_LoadsAndContainsSnapTimeInTitle()
    {
        // Act
        await Page.GotoAsync(BaseUrl);

        // Assert
        StringAssert.Contains("SnapTime", await Page.TitleAsync());
    }
}
