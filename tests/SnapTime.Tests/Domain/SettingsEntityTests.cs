using FluentAssertions;
using SnapTime.Domain.Entities;

namespace SnapTime.Tests.Domain;

public class SettingsEntityTests
{
    [Fact]
    public void Settings_Constructor_SetsDefaultValues()
    {
        var s = new Settings();

        s.Id.Should().Be(0);
        s.ConfidenceThreshold.Should().Be(80);
        s.MaxConcurrency.Should().Be(4);
        s.BatchSize.Should().Be(100);
        s.ImageExtensionsCsv.Should().Be(".jpg,.jpeg");
        s.VideoExtensionsCsv.Should().Be(".mp4,.mov,.avi,.mkv,.webm,.m4v");
        s.OllamaEndpoint.Should().Be("http://localhost:11434");
        s.OllamaModel.Should().Be("qwen2.5-coder:14b");
        s.OllamaTimeoutSeconds.Should().Be(60);
        s.ThumbnailMaxDimension.Should().Be(300);
        s.ThumbnailQuality.Should().Be(80);
    }

    [Fact]
    public void HeuristicConfigEntity_Constructor_SetsDefaultValues()
    {
        var h = new HeuristicConfigEntity();

        h.Id.Should().BeEmpty();
        h.Enabled.Should().BeTrue();
        h.Weight.Should().Be(1.0);
    }
}
