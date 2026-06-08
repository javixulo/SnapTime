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
    public void ScanPanel_RendersCheckboxAndButton()
    {
        var cut = RenderComponent<ScanPanel>();

        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.Should().NotBeNull();
        cut.Markup.Should().Contain("Incluir subcarpetas");

        var button = cut.Find("button");
        button.TextContent.Should().Contain("Escanear");
    }

    [Fact]
    public void ScanPanel_NoFolderSelected_UsesSamplePath()
    {
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = "sample/",
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(expectedJob);

        var cut = RenderComponent<ScanPanel>();

        cut.Find("button").Click();

        _scanClient.Received(1).StartScanAsync(Arg.Is<string>(path =>
            path.Contains("sample", StringComparison.OrdinalIgnoreCase)), Arg.Any<bool>());
    }

    [Fact]
    public void ScanPanel_WithFolderSelected_SendsThatPath()
    {
        var customPath = "/Users/test/photos";
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = customPath,
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(expectedJob);

        var cut = RenderComponent<ScanPanel>(p =>
            p.Add(m => m.SelectedFolderPath, customPath));

        cut.Find("button").Click();

        _scanClient.Received(1).StartScanAsync(Arg.Is<string>(path =>
            path == customPath), Arg.Any<bool>());
    }

    [Fact]
    public void ScanPanel_IncludeSubfoldersDefault_IsTrue()
    {
        var cut = RenderComponent<ScanPanel>();

        var checkboxes = cut.FindAll("input[type=checkbox]");
        checkboxes.Should().HaveCount(1);

        checkboxes[0].HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void ScanPanel_IncludeSubfoldersFalse_SendsFalse()
    {
        var customPath = "/Users/test/photos";
        var expectedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = customPath,
            CreatedAt = DateTime.UtcNow
        };
        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(expectedJob);

        var cut = RenderComponent<ScanPanel>(p =>
            p.Add(m => m.SelectedFolderPath, customPath));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        checkboxes[0].Change(false);

        cut.Find("button").Click();

        _scanClient.Received(1).StartScanAsync(Arg.Any<string>(), false);
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
        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>()).Returns(completedJob);

        var cut = RenderComponent<ScanPanel>();

        cut.Find("button").Click();

        cut.Markup.Should().Contain(jobId.ToString());
        cut.Markup.Should().Contain("10");
    }
}
