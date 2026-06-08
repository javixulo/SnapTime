// [F4-US-005] — Smoketest bUnit: renderiza Home.razor y verifica el layout
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Pages;
using SnapTime.Client.Services;
using SnapTime.Client.Models;

namespace SnapTime.Client.Tests.Pages;

public class HomePageTests : TestContext
{
    private readonly IFilesystemClient _filesystemClient = Substitute.For<IFilesystemClient>();
    private readonly IScanClient _scanClient = Substitute.For<IScanClient>();
    private readonly IPhotoClient _photoClient = Substitute.For<IPhotoClient>();

    public HomePageTests()
    {
        Services.AddSingleton(_filesystemClient);
        Services.AddSingleton(_scanClient);
        Services.AddSingleton(_photoClient);
        Services.AddSingleton(new ApiConfig { BaseUrl = "http://localhost:5000" });
    }

    [Fact]
    public void Home_RendersSnapTimeLayout()
    {
        _filesystemClient.GetDirectoriesAsync(null, Arg.Any<CancellationToken>())
            .Returns(new[] { "Users" });

        var cut = RenderComponent<Home>();

        cut.Markup.Should().Contain("snaptime-layout");
        cut.Markup.Should().Contain("snaptime-left-panel");
        cut.Markup.Should().Contain("snaptime-center-panel");
        cut.Markup.Should().Contain("snaptime-right-panel");
        cut.Markup.Should().Contain("Sistema de archivos");
        cut.Markup.Should().Contain("Users");
    }
}
