// [F7-US-003] bUnit tests for BatchActions.razor — botones de lote habilitados/deshabilitados
// según estado de escaneo y existencia de recomendaciones, más modal de confirmación
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SnapTime.Client.Components;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Components;

public class BatchActionsF7Tests : TestContext
{
    private readonly IScanStateService _scanStateService = Substitute.For<IScanStateService>();
    private readonly IReviewClient _reviewClient = Substitute.For<IReviewClient>();

    public BatchActionsF7Tests()
    {
        Services.AddSingleton(_scanStateService);
        Services.AddSingleton(_reviewClient);
    }

    // ──────────────────────────────────────────────
    // Scan activo — todos los botones deshabilitados
    // ──────────────────────────────────────────────

    [Fact]
    public void ScanActivo_BotonesLoteDeshabilitados()
    {
        // Arrange: scan en ejecución
        _scanStateService.IsScanning.Returns(true);
        _scanStateService.HasCompletedScan.Returns(false);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Assert: todos los botones de lote deben tener la clase "disabled"
        var acceptAllBtn = cut.Find(".btn-accept-all");
        acceptAllBtn.ClassName.Should().Contain("disabled",
            "Aceptar Todo debe estar deshabilitado durante el escaneo");

        var rejectAllBtn = cut.Find(".btn-reject-all");
        rejectAllBtn.ClassName.Should().Contain("disabled",
            "Rechazar Todo debe estar deshabilitado durante el escaneo");

        var acceptTotalBtn = cut.Find(".btn-accept-total");
        acceptTotalBtn.ClassName.Should().Contain("disabled",
            "Aceptar Total debe estar deshabilitado durante el escaneo");

        var rejectTotalBtn = cut.Find(".btn-reject-total");
        rejectTotalBtn.ClassName.Should().Contain("disabled",
            "Rechazar Total debe estar deshabilitado durante el escaneo");
    }

    // ──────────────────────────────────────────────
    // Scan completado, sin recomendaciones
    // ──────────────────────────────────────────────

    [Fact]
    public void ScanCompletado_SinRecomendaciones_BotonesDeshabilitados()
    {
        // Arrange: scan completado pero no hay archivos con recomendaciones
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, false));

        // Assert
        var acceptAllBtn = cut.Find(".btn-accept-all");
        acceptAllBtn.ClassName.Should().Contain("disabled",
            "Aceptar Todo debe estar deshabilitado si no hay recomendaciones");

        var rejectAllBtn = cut.Find(".btn-reject-all");
        rejectAllBtn.ClassName.Should().Contain("disabled",
            "Rechazar Todo debe estar deshabilitado si no hay recomendaciones");

        var acceptTotalBtn = cut.Find(".btn-accept-total");
        acceptTotalBtn.ClassName.Should().Contain("disabled",
            "Aceptar Total debe estar deshabilitado si no hay recomendaciones");

        var rejectTotalBtn = cut.Find(".btn-reject-total");
        rejectTotalBtn.ClassName.Should().Contain("disabled",
            "Rechazar Total debe estar deshabilitado si no hay recomendaciones");
    }

    // ──────────────────────────────────────────────
    // Scan completado + con recomendaciones
    // ──────────────────────────────────────────────

    [Fact]
    public void ScanCompletado_ConRecomendaciones_BotonesHabilitados()
    {
        // Arrange: scan completado y hay archivos con recomendaciones
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Assert: ningún botón debe tener la clase "disabled"
        var acceptAllBtn = cut.Find(".btn-accept-all");
        acceptAllBtn.ClassName.Should().NotContain("disabled",
            "Aceptar Todo debe estar habilitado: scan completo + hay recomendaciones");

        var rejectAllBtn = cut.Find(".btn-reject-all");
        rejectAllBtn.ClassName.Should().NotContain("disabled",
            "Rechazar Todo debe estar habilitado: scan completo + hay recomendaciones");

        var acceptTotalBtn = cut.Find(".btn-accept-total");
        acceptTotalBtn.ClassName.Should().NotContain("disabled",
            "Aceptar Total debe estar habilitado: scan completo + hay recomendaciones");

        var rejectTotalBtn = cut.Find(".btn-reject-total");
        rejectTotalBtn.ClassName.Should().NotContain("disabled",
            "Rechazar Total debe estar habilitado: scan completo + hay recomendaciones");
    }

    // ──────────────────────────────────────────────
    // Modal de confirmación
    // ──────────────────────────────────────────────

    [Fact]
    public void ClickAceptarTodo_MuestraModalConfirmacion()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: hacer clic en Aceptar Todo
        cut.Find(".btn-accept-all").Click();

        // Assert: debe aparecer el modal con texto de confirmación
        cut.Markup.Should().Contain("confirm-modal",
            "debe existir un elemento con clase confirm-modal");
        cut.Markup.Should().Contain("Se aprobarán",
            "el modal debe contener el texto 'Se aprobarán' en el resumen de confirmación");
    }

    [Fact]
    public void ClickRechazarTodo_MuestraModalConfirmacion()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: hacer clic en Rechazar Todo
        cut.Find(".btn-reject-all").Click();

        // Assert: debe aparecer el modal con texto de confirmación
        cut.Markup.Should().Contain("confirm-modal",
            "debe existir un elemento con clase confirm-modal");
        cut.Markup.Should().Contain("Se rechazarán",
            "el modal debe contener el texto 'Se rechazarán' en el resumen de confirmación");
    }

    // ──────────────────────────────────────────────
    // Llamadas a la API
    // ──────────────────────────────────────────────

    [Fact]
    public void ConfirmarAceptarTodo_LlamaAPIConScopeFolder()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: clic Aceptar Todo → se abre modal → clic Confirmar
        cut.Find(".btn-accept-all").Click();
        cut.Find(".confirm-modal .btn-confirm").Click();

        // Assert: la API debe recibir scope "folder" con status "approved"
        _reviewClient.Received(1).BatchReviewAsync(
            Arg.Is<string>(s => s == "folder"),
            Arg.Is<string>(s => s == "approved"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ConfirmarRechazarTodo_LlamaAPIConScopeFolder()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: clic Rechazar Todo → se abre modal → clic Confirmar
        cut.Find(".btn-reject-all").Click();
        cut.Find(".confirm-modal .btn-confirm").Click();

        // Assert: la API debe recibir scope "folder" con status "rejected"
        _reviewClient.Received(1).BatchReviewAsync(
            Arg.Is<string>(s => s == "folder"),
            Arg.Is<string>(s => s == "rejected"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AceptarTotal_LlamaAPIConScopeTotal()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: clic Aceptar Total → se abre modal → clic Confirmar
        cut.Find(".btn-accept-total").Click();
        cut.Find(".confirm-modal .btn-confirm").Click();

        // Assert: la API debe recibir scope "total" con status "approved"
        _reviewClient.Received(1).BatchReviewAsync(
            Arg.Is<string>(s => s == "total"),
            Arg.Is<string>(s => s == "approved"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RechazarTotal_LlamaAPIConScopeTotal()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: clic Rechazar Total → se abre modal → clic Confirmar
        cut.Find(".btn-reject-total").Click();
        cut.Find(".confirm-modal .btn-confirm").Click();

        // Assert: la API debe recibir scope "total" con status "rejected"
        _reviewClient.Received(1).BatchReviewAsync(
            Arg.Is<string>(s => s == "total"),
            Arg.Is<string>(s => s == "rejected"),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelarModal_NoLlamaAPI()
    {
        // Arrange
        _scanStateService.IsScanning.Returns(false);
        _scanStateService.HasCompletedScan.Returns(true);

        var cut = RenderComponent<BatchActions>(p =>
            p.Add(c => c.HasRecommendations, true));

        // Act: clic Aceptar Todo → se abre modal → clic Cancelar del modal
        cut.Find(".btn-accept-all").Click();
        cut.Find(".confirm-modal .btn-cancel").Click();

        // Assert: la API NO debe haber sido llamada
        _reviewClient.DidNotReceiveWithAnyArgs().BatchReviewAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
