// [F4-US-000] -- bUnit tests for ScanPanel.razor
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class ScanPanelTests : TestContext
{
    private readonly IScanClient _scanClient = Substitute.For<IScanClient>();

    public ScanPanelTests()
    {
        Services.AddSingleton(_scanClient);
    }

    [Fact]
    public void ScanPanel_RendersInputAndButton()
    {
        var cut = RenderComponent<ScanPanel>();

        cut.Find("input").Should().NotBeNull();
        var button = cut.Find("button");
        button.TextContent.Should().Contain("Escanear");
    }

    [Fact]
    public void ScanPanel_EmptyInput_UsesDefaultPath()
    {
        var cut = RenderComponent<ScanPanel>();
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = "sample/",
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>()).Returns(expectedJob);

        cut.FindAll("button")[0].Click();

        _scanClient.Received(1).StartScanAsync(Arg.Is<string>(path =>
            path.Contains("sample", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ScanPanel_WithCustomPath_SendsThatPath()
    {
        var cut = RenderComponent<ScanPanel>();
        var customPath = "/Users/test/photos";
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = customPath,
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>()).Returns(expectedJob);

        cut.Find("input").Change(customPath);
        cut.FindAll("button")[0].Click();

        _scanClient.Received(1).StartScanAsync(Arg.Is<string>(path =>
            path == customPath));
    }

    [Fact]
    public void ScanPanel_ShowsProgressAfterScan()
    {
        var jobId = Guid.NewGuid();
        var completedJob = new ScanJobDto
        {
            Id = jobId,
            Status = "Completed",
            RootPath = "sample/",
            TotalFiles = 25,
            ProcessedFiles = 10,
            ErrorCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>()).Returns(completedJob);

        var cut = RenderComponent<ScanPanel>();

        cut.FindAll("button")[0].Click();

        cut.Markup.Should().Contain(jobId.ToString());
        cut.Markup.Should().Contain("10");
    }

    [Fact]
    public void ScanPanel_WhitespaceInput_UsesDefaultPath()
    {
        var cut = RenderComponent<ScanPanel>();
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = "sample/",
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>()).Returns(expectedJob);

        cut.Find("input").Change("   ");
        cut.FindAll("button")[0].Click();

        _scanClient.Received(1).StartScanAsync(Arg.Is<string>(path =>
            path.Contains("sample", StringComparison.OrdinalIgnoreCase)));
    }

}
