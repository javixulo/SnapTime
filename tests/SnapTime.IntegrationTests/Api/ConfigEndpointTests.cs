// [F10-US-003] Integration tests for GET/PUT /api/config
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SnapTime.Domain.Config;
using SnapTime.Domain.Entities;
using SnapTime.Server.Models;

namespace SnapTime.IntegrationTests.Api;

[Collection("SqliteIntegration")]
public class ConfigEndpointTests
{
    private readonly SqliteDbFixture _fixture;
    private readonly HttpClient _client;

    public ConfigEndpointTests(SqliteDbFixture fixture)
    {
        _fixture = fixture;
        _fixture.ResetDatabase();
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetConfig_ReturnsDefaultValues()
    {
        var response = await _client.GetAsync("/api/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<SnapTimeConfig>();
        config.Should().NotBeNull();
        config!.Analysis.ConfidenceThreshold.Should().Be(70);
        config.Ollama.Model.Should().Be("qwen2.5-coder:14b");
        config.Thumbnails.MaxDimension.Should().Be(300);
        config.Heuristics.Should().HaveCount(6);
        config.Database.Path.Should().NotBeNullOrEmpty();
        config.Logging.Level.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PutConfig_UpdatesValues()
    {
        // Arrange: seed a settings row first
        using (var db = _fixture.CreateContext())
        {
            var existing = db.Settings.Find(1);
            if (existing is null)
            {
                db.Settings.Add(new Settings { Id = 1 });
                db.SaveChanges();
            }
        }

        var request = new ConfigUpdateRequest(
            ConfidenceThreshold: 50,
            MaxConcurrency: 2,
            BatchSize: 25,
            ImageExtensionsCsv: ".png,.jpg",
            VideoExtensionsCsv: ".webm",
            OllamaEndpoint: "http://192.168.1.1:11434",
            OllamaModel: "llama3.1",
            OllamaTimeoutSeconds: 120,
            ThumbnailMaxDimension: 600,
            ThumbnailQuality: 90,
            Heuristics:
            [
                new HeuristicConfigDto("H-001", false, 0.5),
            ]);

        var response = await _client.PutAsJsonAsync("/api/config", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<SnapTimeConfig>();
        config.Should().NotBeNull();
        config!.Analysis.ConfidenceThreshold.Should().Be(50);
        config.Analysis.MaxConcurrency.Should().Be(2);
        config.Analysis.BatchSize.Should().Be(25);
        config.Ollama.Endpoint.Should().Be("http://192.168.1.1:11434");
        config.Ollama.Model.Should().Be("llama3.1");
        config.Thumbnails.MaxDimension.Should().Be(600);
        config.Thumbnails.Quality.Should().Be(90);

        var h001 = config.Heuristics.Should().ContainSingle(h => h.Id == "H-001");
        h001.Subject.Enabled.Should().BeFalse();
        h001.Subject.Weight.Should().Be(0.5);
    }

    [Fact]
    public async Task PutConfig_WithInvalidValues_Clamps()
    {
        using (var db = _fixture.CreateContext())
        {
            var existing = db.Settings.Find(1);
            if (existing is null)
            {
                db.Settings.Add(new Settings { Id = 1 });
                db.SaveChanges();
            }
        }

        var request = new ConfigUpdateRequest(
            ConfidenceThreshold: -10,
            MaxConcurrency: 0,
            BatchSize: 0,
            ImageExtensionsCsv: null,
            VideoExtensionsCsv: null,
            OllamaEndpoint: null,
            OllamaModel: null,
            OllamaTimeoutSeconds: 0,
            ThumbnailMaxDimension: 10,
            ThumbnailQuality: 200,
            Heuristics: null);

        var response = await _client.PutAsJsonAsync("/api/config", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var config = await response.Content.ReadFromJsonAsync<SnapTimeConfig>();
        config.Should().NotBeNull();
        config!.Analysis.ConfidenceThreshold.Should().Be(0);
        config.Analysis.MaxConcurrency.Should().Be(1); // clamped to min 1
        config.Analysis.BatchSize.Should().Be(1);      // clamped to min 1
        config.Ollama.TimeoutSeconds.Should().Be(1);    // clamped to min 1
        config.Thumbnails.MaxDimension.Should().Be(50); // clamped to min 50
        config.Thumbnails.Quality.Should().Be(100);     // clamped to max 100
    }

    [Fact]
    public async Task PutConfig_Roundtrips()
    {
        using (var db = _fixture.CreateContext())
        {
            var existing = db.Settings.Find(1);
            if (existing is null)
            {
                db.Settings.Add(new Settings { Id = 1 });
                db.SaveChanges();
            }
        }

        var request = new ConfigUpdateRequest(
            ConfidenceThreshold: 60,
            MaxConcurrency: 6,
            BatchSize: 200,
            ImageExtensionsCsv: ".jpg,.jpeg,.png,.webp",
            VideoExtensionsCsv: ".mp4,.mkv,.webm,.avi,.mov",
            OllamaEndpoint: "http://localhost:11434",
            OllamaModel: "qwen2.5-coder:7b",
            OllamaTimeoutSeconds: 30,
            ThumbnailMaxDimension: 150,
            ThumbnailQuality: 60,
            Heuristics:
            [
                new HeuristicConfigDto("H-001", true, 1.0),
                new HeuristicConfigDto("H-002", false, 0.0),
                new HeuristicConfigDto("H-003", true, 0.8),
            ]);

        // Write
        var putResponse = await _client.PutAsJsonAsync("/api/config", request);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Read back
        var getResponse = await _client.GetAsync("/api/config");
        var config = await getResponse.Content.ReadFromJsonAsync<SnapTimeConfig>();

        config!.Analysis.ConfidenceThreshold.Should().Be(60);
        config.Analysis.MaxConcurrency.Should().Be(6);
        config.Heuristics.Should().Contain(h => h.Id == "H-001" && h.Enabled);
        config.Heuristics.Should().Contain(h => h.Id == "H-002" && !h.Enabled);
    }
}
