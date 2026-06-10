// [F7-US-003] Unit tests for ScanStateService — estado de escaneo singleton
using FluentAssertions;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Services;

public class ScanStateServiceTests
{
    [Fact]
    public void EstadoInicial_IsScanningFalse_HasCompletedScanFalse()
    {
        // Arrange & Act
        var service = new ScanStateService();

        // Assert
        service.IsScanning.Should().BeFalse(
            "el estado inicial debe ser IsScanning = false");
        service.HasCompletedScan.Should().BeFalse(
            "el estado inicial debe ser HasCompletedScan = false");
    }

    [Fact]
    public void NotificarScanStart_IsScanningTrue()
    {
        // Arrange
        var service = new ScanStateService();

        // Act
        service.NotifyScanStart();

        // Assert
        service.IsScanning.Should().BeTrue(
            "después de NotifyScanStart, IsScanning debe ser true");
        service.HasCompletedScan.Should().BeFalse(
            "HasCompletedScan no debe cambiar al iniciar un scan");
    }

    [Fact]
    public void NotificarScanComplete_IsScanningFalse_HasCompletedScanTrue()
    {
        // Arrange
        var service = new ScanStateService();
        service.NotifyScanStart();

        // Act
        service.NotifyScanComplete();

        // Assert
        service.IsScanning.Should().BeFalse(
            "después de NotifyScanComplete, IsScanning debe ser false");
        service.HasCompletedScan.Should().BeTrue(
            "después de NotifyScanComplete, HasCompletedScan debe ser true");
    }

    [Fact]
    public void NotificarScanCancelled_IsScanningFalse_HasCompletedScanFalse()
    {
        // Arrange
        var service = new ScanStateService();
        service.NotifyScanStart();

        // Act
        service.NotifyScanCancelled();

        // Assert
        service.IsScanning.Should().BeFalse(
            "después de NotifyScanCancelled, IsScanning debe ser false");
        service.HasCompletedScan.Should().BeFalse(
            "después de NotifyScanCancelled, HasCompletedScan debe seguir false");
    }

    [Fact]
    public void Reset_DespuesDeCompletado_RestableceEstadoInicial()
    {
        // Arrange
        var service = new ScanStateService();
        service.NotifyScanStart();
        service.NotifyScanComplete();

        // Act
        service.Reset();

        // Assert
        service.IsScanning.Should().BeFalse();
        service.HasCompletedScan.Should().BeFalse(
            "Reset debe volver HasCompletedScan a false, simulando cambio de carpeta");
    }

    [Fact]
    public void StateChanged_SeDisparaAlNotificarScanStart()
    {
        // Arrange
        var service = new ScanStateService();
        var fired = false;
        service.StateChanged += () => fired = true;

        // Act
        service.NotifyScanStart();

        // Assert
        fired.Should().BeTrue("StateChanged debe dispararse al iniciar scan");
    }

    [Fact]
    public void StateChanged_SeDisparaAlNotificarScanComplete()
    {
        // Arrange
        var service = new ScanStateService();
        service.NotifyScanStart();
        var fired = false;
        service.StateChanged += () => fired = true;

        // Act
        service.NotifyScanComplete();

        // Assert
        fired.Should().BeTrue("StateChanged debe dispararse al completar scan");
    }

    [Fact]
    public void StateChanged_SeDisparaAlResetear()
    {
        // Arrange
        var service = new ScanStateService();
        service.NotifyScanStart();
        service.NotifyScanComplete();
        var fired = false;
        service.StateChanged += () => fired = true;

        // Act
        service.Reset();

        // Assert
        fired.Should().BeTrue("StateChanged debe dispararse al resetear estado");
    }
}
