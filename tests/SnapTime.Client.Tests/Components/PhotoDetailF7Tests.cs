// [F7-US-003] bUnit tests for PhotoDetail.razor — botones habilitados/deshabilitados
// según estado de escaneo y existencia de SuggestedDate
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class PhotoDetailF7Tests : TestContext
{
    private readonly IScanStateService _scanStateService = Substitute.For<IScanStateService>();
    private readonly IPhotoClient _photoClient = Substitute.For<IPhotoClient>();
    private readonly MediaAssetDetailDto _detailConSugerencia;

    public PhotoDetailF7Tests()
    {
        Services.AddSingleton(_scanStateService);
        Services.AddSingleton(_photoClient);
        Services.AddSingleton(new ApiConfig { BaseUrl = "http://localhost:3000" });

        _detailConSugerencia = new MediaAssetDetailDto
        {
            Id = Guid.NewGuid(),
            FilePath = "/test/vacation.jpg",
            FileName = "vacation.jpg",
            MediaType = "Image",
            FileSize = 1024,
            DateTimeOriginal = new DateTime(2024, 8, 15, 10, 0, 0),
            CreateDate = new DateTime(2024, 8, 15, 10, 0, 0),
            ModifyDate = new DateTime(2024, 8, 16, 12, 0, 0),
            FileCreatedAt = new DateTime(2024, 8, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 8, 16, 12, 0, 0),
            ConfidenceScore = 85,
            SuggestedDate = new DateTime(2024, 8, 15),
            SuggestedByHeuristic = "FilenameHeuristic",
            Evidence =
            [
                new EvidenceDto
                {
                    HeuristicId = "H006",
                    HeuristicName = "Filename heuristic",
                    Weight = 0.8,
                    Direction = "positive",
                    Description = "Nombre contiene fecha 20240815"
                }
            ]
        };
    }

    // ──────────────────────────────────────────────
    // Scan activo — botones deshabilitados
    // ──────────────────────────────────────────────

    [Fact]
    public void ConScanActivo_BotonesDeshabilitados()
    {
        // Arrange: el scan está activo, el archivo tiene SuggestedDate
        _scanStateService.IsScanning.Returns(true);
        _scanStateService.HasCompletedScan.Returns(false);

        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_detailConSugerencia);

        var cut = RenderComponent<PhotoDetail>(p =>
            p.Add(c => c.SelectedAssetId, _detailConSugerencia.Id));

        // Assert: ambos botones deben tener la clase CSS "disabled"
        var acceptBtn = cut.Find(".btn-accept");
        acceptBtn.ClassName.Should().Contain("disabled",
            "el botón Aceptar debe estar deshabilitado durante el escaneo");

        var rejectBtn = cut.Find(".btn-reject");
        rejectBtn.ClassName.Should().Contain("disabled",
            "el botón Rechazar debe estar deshabilitado durante el escaneo");
    }

    // ──────────────────────────────────────────────
    // Scan completado, archivo sin SuggestedDate
    // ──────────────────────────────────────────────

    [Fact]
    public void ScanCompletado_SinSuggestedDate_BotonesDeshabilitados()
    {
        // Arrange: scan completado pero el archivo no tiene fecha sugerida
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var detailSinSugerencia = new MediaAssetDetailDto
        {
            Id = Guid.NewGuid(),
            FilePath = "/test/sin_sugerencia.jpg",
            FileName = "sin_sugerencia.jpg",
            MediaType = "Image",
            FileSize = 2048,
            DateTimeOriginal = new DateTime(2024, 9, 10, 10, 0, 0),
            CreateDate = new DateTime(2024, 9, 10, 10, 0, 0),
            ModifyDate = new DateTime(2024, 9, 11, 12, 0, 0),
            FileCreatedAt = new DateTime(2024, 9, 10, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 9, 11, 12, 0, 0),
            ConfidenceScore = 35,
            SuggestedDate = null,
            Evidence = []
        };

        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(detailSinSugerencia);

        var cut = RenderComponent<PhotoDetail>(p =>
            p.Add(c => c.SelectedAssetId, detailSinSugerencia.Id));

        // Assert: botones deshabilitados porque no hay SuggestedDate
        var acceptBtn = cut.Find(".btn-accept");
        acceptBtn.ClassName.Should().Contain("disabled",
            "el botón Aceptar debe estar deshabilitado si el archivo no tiene SuggestedDate");

        var rejectBtn = cut.Find(".btn-reject");
        rejectBtn.ClassName.Should().Contain("disabled",
            "el botón Rechazar debe estar deshabilitado si el archivo no tiene SuggestedDate");
    }

    // ──────────────────────────────────────────────
    // Scan completado + archivo con SuggestedDate
    // ──────────────────────────────────────────────

    [Fact]
    public void ScanCompletado_ConSuggestedDate_BotonesHabilitados()
    {
        // Arrange: scan completado y el archivo tiene fecha sugerida
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        _photoClient.GetAssetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(_detailConSugerencia);

        var cut = RenderComponent<PhotoDetail>(p =>
            p.Add(c => c.SelectedAssetId, _detailConSugerencia.Id));

        // Assert: los botones NO deben tener la clase "disabled"
        var acceptBtn = cut.Find(".btn-accept");
        acceptBtn.ClassName.Should().NotContain("disabled",
            "el botón Aceptar debe estar habilitado: scan completado + SuggestedDate presente");

        var rejectBtn = cut.Find(".btn-reject");
        rejectBtn.ClassName.Should().NotContain("disabled",
            "el botón Rechazar debe estar habilitado: scan completado + SuggestedDate presente");
    }
}
