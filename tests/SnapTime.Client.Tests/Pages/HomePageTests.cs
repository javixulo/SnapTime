// [F0-US-010] — Smoketest bUnit: renderiza Home.razor y verifica el título
using Bunit;
using SnapTime.Client.Pages;

namespace SnapTime.Client.Tests.Pages;

public class HomePageTests : TestContext
{
    [Fact]
    public void Home_RendersWithHelloWorldHeading_ContainsExpectedText()
    {
        // Arrange & Act
        var cut = RenderComponent<Home>();

        // Assert
        cut.Find("h1").MarkupMatches("<h1>Hello, world!</h1>");
    }
}
