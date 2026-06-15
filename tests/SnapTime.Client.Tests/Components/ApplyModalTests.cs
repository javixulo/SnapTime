// [F8-US-005] bUnit tests for ApplyModal component
using System.Net.Http;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using SnapTime.Client.Components;
using SnapTime.Client.Models;

namespace SnapTime.Client.Tests.Components;

public class ApplyModalTests : TestContext
{
    public ApplyModalTests()
    {
        Services.AddSingleton(new HttpClient());
    }

    [Fact]
    public void ApplyModal_WhenHidden_DoesNotRenderContent()
    {
        var cut = RenderComponent<ApplyModal>(parameters => parameters
            .Add(p => p.IsVisible, false));

        cut.Markup.Should().NotContain("Aplicar cambios");
    }

    [Fact]
    public void ApplyModal_WhenVisibleWithAssets_ShowsList()
    {
        var assets = new List<MediaAssetDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FileName = "foto.jpg",
                DateTimeOriginal = new DateTime(2024, 1, 15),
                SuggestedDate = new DateTime(2025, 4, 10, 5, 0, 0)
            }
        };

        var cut = RenderComponent<ApplyModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Assets, assets));

        cut.Markup.Should().Contain("foto.jpg");
        cut.Markup.Should().Contain("15/01/2024");
        cut.Markup.Should().Contain("10/04/2025");
        cut.Markup.Should().Contain("Aplicar");
    }

    [Fact]
    public void ApplyModal_WhenVisibleWithNoAssets_ShowsEmptyMessage()
    {
        var cut = RenderComponent<ApplyModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Assets, new List<MediaAssetDto>()));

        cut.Markup.Should().Contain("No hay archivos seleccionados");
    }

    [Fact]
    public void ApplyModal_ApplyButtonDisabled_WhenNoSuggestedDate()
    {
        var assets = new List<MediaAssetDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FileName = "foto.jpg",
                DateTimeOriginal = new DateTime(2024, 1, 15),
                SuggestedDate = null
            }
        };

        var cut = RenderComponent<ApplyModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Assets, assets));

        var applyButton = cut.Find("button.btn-primary");
        applyButton.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void ApplyModal_CancelButton_ClosesModal()
    {
        var closed = false;
        var assets = new List<MediaAssetDto>
        {
            new() { Id = Guid.NewGuid(), FileName = "test.jpg" }
        };

        var cut = RenderComponent<ApplyModal>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Assets, assets)
            .Add(p => p.OnClose, EventCallback.Factory.Create(this, () => closed = true)));

        var cancelButton = cut.Find("button.btn-secondary");
        cancelButton.Click();

        closed.Should().BeTrue();
    }
}
