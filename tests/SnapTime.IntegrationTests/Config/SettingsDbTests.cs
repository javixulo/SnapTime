using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SnapTime.Domain.Entities;
using SnapTime.Infrastructure.Data;

namespace SnapTime.IntegrationTests.Config;

[Collection("SqliteIntegration")]
public class SettingsDbTests
{
    private readonly SqliteDbFixture _fixture;

    public SettingsDbTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
    }

    [Fact]
    public void SettingsTable_Exists_AfterMigration()
    {
        using var db = _fixture.CreateContext();

        // Table exists after migration — no exception
        var count = db.Settings.Count();
        count.Should().Be(0);
    }

    [Fact]
    public void Settings_ReadWrite_Roundtrips()
    {
        using var db = _fixture.CreateContext();

        db.Settings.Add(new Settings
        {
            Id = 1,
            ConfidenceThreshold = 95,
            MaxConcurrency = 2,
            BatchSize = 50,
            ImageExtensionsCsv = ".png,.jpg",
            VideoExtensionsCsv = ".mp4",
            OllamaEndpoint = "http://192.168.1.1:11434",
            OllamaModel = "llama3",
            OllamaTimeoutSeconds = 120,
            ThumbnailMaxDimension = 600,
            ThumbnailQuality = 50
        });
        db.SaveChanges();

        using var db2 = _fixture.CreateContext();
        var loaded = db2.Settings.Find(1);

        loaded.Should().NotBeNull();
        loaded!.ConfidenceThreshold.Should().Be(95);
        loaded.MaxConcurrency.Should().Be(2);
        loaded.BatchSize.Should().Be(50);
        loaded.ImageExtensionsCsv.Should().Be(".png,.jpg");
        loaded.VideoExtensionsCsv.Should().Be(".mp4");
        loaded.OllamaEndpoint.Should().Be("http://192.168.1.1:11434");
        loaded.OllamaModel.Should().Be("llama3");
        loaded.OllamaTimeoutSeconds.Should().Be(120);
        loaded.ThumbnailMaxDimension.Should().Be(600);
        loaded.ThumbnailQuality.Should().Be(50);
    }

    [Fact]
    public void SingleRow_Update_ModifiesExisting()
    {
        using var db = _fixture.CreateContext();

        db.Settings.Add(new Settings { Id = 1, ConfidenceThreshold = 80 });
        db.SaveChanges();

        // Detach the tracked entity, then update via Update()
        db.ChangeTracker.Clear();
        db.Settings.Update(new Settings { Id = 1, ConfidenceThreshold = 90 });
        db.SaveChanges();

        db.Settings.Count().Should().Be(1);
        db.Settings.Find(1)!.ConfidenceThreshold.Should().Be(90);
    }

    [Fact]
    public void HeuristicConfig_CanBeCreatedAndRead()
    {
        using var db = _fixture.CreateContext();

        db.HeuristicConfigs.Add(new HeuristicConfigEntity
        {
            Id = "H-007",
            Enabled = true,
            Weight = 0.8
        });
        db.SaveChanges();

        using var db2 = _fixture.CreateContext();
        var loaded = db2.HeuristicConfigs.Find("H-007");

        loaded.Should().NotBeNull();
        loaded!.Enabled.Should().BeTrue();
        loaded.Weight.Should().Be(0.8);
    }
}
