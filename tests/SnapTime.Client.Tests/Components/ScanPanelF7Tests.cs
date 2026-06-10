// [F7-US-001] -- bUnit tests for ScanPanel: progreso, cancelación y reescaneo
using System.Net.Http;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class ScanPanelF7Tests : TestContext
{
    private readonly IScanClient _scanClient = Substitute.For<IScanClient>();
    private readonly IScanStateService _scanStateService = Substitute.For<IScanStateService>();

    public ScanPanelF7Tests()
    {
        Services.AddSingleton(_scanClient);
        Services.AddSingleton(_scanStateService);
    }

    [Fact]
    public void ScanPanel_BotonEscanear_VisibleYHabilitadoEnIdle()
    {
        // Arrange & Act
        var cut = RenderComponent<ScanPanel>();

        // Assert
        var scanButton = cut.Find("button");
        scanButton.TextContent.Should().Contain("Escanear");
        scanButton.HasAttribute("disabled").Should().BeFalse(
            "el botón Escanear debe estar habilitado en estado idle");
    }

    [Fact]
    public void ScanPanel_ClickEscanear_DeshabilitaEscanearMuestraCancelarYProgreso()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var runningJob = new ScanJobDto
        {
            Id = jobId,
            Status = "Running",
            RootPath = "test/",
            TotalFiles = 10,
            ProcessedFiles = 0,
            CreatedAt = DateTime.UtcNow
        };
        var progressJob = new ScanJobDto
        {
            Id = jobId,
            Status = "Running",
            RootPath = "test/",
            TotalFiles = 10,
            ProcessedFiles = 3,
            CreatedAt = DateTime.UtcNow
        };

        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(runningJob);
        _scanClient.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(progressJob);

        var cut = RenderComponent<ScanPanel>();

        // Act
        cut.Find("button").Click();

        // Assert: esperar a que aparezca el botón Cancelar (el scan inició)
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Cancelar"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        // El ScanStateService debe haber sido notificado del inicio
        _scanStateService.Received(1).NotifyScanStart();

        // El botón Escanear debe estar deshabilitado
        var scanButton = cut.Find("button");
        scanButton.HasAttribute("disabled").Should().BeTrue(
            "el botón Escanear debe deshabilitarse durante el escaneo");

        // El botón Cancelar debe estar visible
        cut.Markup.Should().Contain("Cancelar",
            "debe aparecer un botón Cancelar durante el escaneo");

        // Debe mostrar progreso: "Procesando N de M archivos" o similar
        cut.Markup.Should().Contain("3",
            "el progreso debe mostrar los archivos procesados");
        cut.Markup.Should().Contain("10",
            "el progreso debe mostrar el total de archivos");
    }

    [Fact]
    public void ScanPanel_ClickCancelar_RehabilitaEscanearOcultaCancelar()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var runningJob = new ScanJobDto
        {
            Id = jobId,
            Status = "Running",
            RootPath = "test/",
            TotalFiles = 10,
            ProcessedFiles = 2,
            CreatedAt = DateTime.UtcNow
        };

        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(runningJob);
        _scanClient.GetJobAsync(jobId, Arg.Any<CancellationToken>())
            .Returns(runningJob);

        var cut = RenderComponent<ScanPanel>();

        // Act: iniciar escaneo
        cut.Find("button").Click();

        // Esperar a que aparezca Cancelar (el polling basado en timer debe dispararse)
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Cancelar"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        // Hacer clic en Cancelar
        var cancelButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Cancelar"));
        cancelButton.Click();

        // Assert: esperar a que desaparezca Cancelar y aparezca Cancelled
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Cancelled"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        // El ScanStateService debe haber sido notificado de la cancelación
        _scanStateService.Received(1).NotifyScanCancelled();

        // El botón Escanear debe estar rehabilitado
        var scanButton = cut.Find("button");
        scanButton.HasAttribute("disabled").Should().BeFalse(
            "el botón Escanear debe rehabilitarse tras cancelar");

        // El botón Cancelar ya no debe estar presente
        var cancelButtons = cut.FindAll("button")
            .Where(b => b.TextContent.Contains("Cancelar"));
        cancelButtons.Should().BeEmpty(
            "el botón Cancelar debe desaparecer tras cancelar");

        // Debe mostrar estado Cancelled
        cut.Markup.Should().Contain("Cancelled",
            "el estado visible debe ser Cancelled tras cancelar");
    }

    [Fact]
    public void ScanPanel_EscanearCarpetaYaEscaneada_LlamaPOSTJobsReescaneo()
    {
        // Arrange
        var folderPath = "/Users/test/photos";
        var completedJob = new ScanJobDto
        {
            Id = Guid.NewGuid(),
            Status = "Completed",
            RootPath = folderPath,
            TotalFiles = 5,
            ProcessedFiles = 5,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(completedJob);
        _scanClient.GetJobAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(completedJob);

        var cut = RenderComponent<ScanPanel>(p =>
            p.Add(m => m.SelectedFolderPath, folderPath));

        // Act: primer escaneo
        cut.Find("button").Click();
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Completed"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        // El primer scan debe haber invocado StartScanAsync una vez
        _scanClient.Received(1).StartScanAsync(
            Arg.Is<string>(p => p == folderPath),
            Arg.Any<bool>());

        // Act: segundo escaneo sobre la misma carpeta (reescaneo forzado)
        cut.Find("button").Click();
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Completed"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        // Assert: StartScanAsync debe haberse invocado dos veces en total
        _scanClient.Received(2).StartScanAsync(
            Arg.Is<string>(p => p == folderPath),
            Arg.Any<bool>());
    }

    [Fact]
    public void ScanPanel_ErrorDeAPI_MuestraMensajeError()
    {
        // Arrange
        _scanClient.StartScanAsync(Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.FromException<ScanJobDto?>(new HttpRequestException("Connection refused")));

        var cut = RenderComponent<ScanPanel>();

        // Act
        cut.Find("button").Click();

        // Assert
        cut.WaitForState(() =>
        {
            try { return cut.Markup.Contains("Error"); }
            catch { return false; }
        }, TimeSpan.FromSeconds(3));

        cut.Markup.Should().Contain("Connection refused",
            "debe mostrar el mensaje de error de la API");
        cut.Markup.Should().Contain("error",
            "el contenedor de error debe tener clase o texto indicativo");
    }
}
