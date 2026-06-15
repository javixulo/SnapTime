// [F4-US-000 / F7-US-001] ScanPanel code-behind — Timer-based polling, cancel, re-scan
using Microsoft.AspNetCore.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Components;

public partial class ScanPanel : IAsyncDisposable
{
    private CancellationTokenSource? _pollingCts;
    private IScanStateService? _scanStateService;

    [Inject]
    private IScanClient ScanClient { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Parameter]
    public string? SelectedFolderPath { get; set; } = null;

    private bool _includeSubfolders = true;
    private bool _isScanning;
    private ScanJobDto? _currentJob;
    private string? _error;
    private string _state = "idle";
    private int _processed;
    private int _total;

    protected override void OnInitialized()
    {
        _scanStateService = ServiceProvider.GetService<IScanStateService>();
    }

    public ValueTask DisposeAsync()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task StartScanAsync()
    {
        _error = null;
        _currentJob = null;
        _state = "idle";
        _isScanning = true;
        _scanStateService?.NotifyScanStart();

        try
        {
            var path = SelectedFolderPath ?? "sample/";
            _currentJob = await ScanClient.StartScanAsync(path, _includeSubfolders);

            if (_currentJob is null)
            {
                _error = "No se pudo iniciar el escaneo";
                _isScanning = false;
                return;
            }

            _state = "scanning";
            _processed = _currentJob.ProcessedFiles;
            _total = _currentJob.TotalFiles;

            // If the job already completed/cancelled/errored, reflect that immediately
            if (_currentJob.Status is "Completed")
            {
                _state = "Completed";
                _isScanning = false;
                _scanStateService?.NotifyScanComplete();
                return;
            }
            if (_currentJob.Status is "Cancelled")
            {
                _state = "Cancelled";
                _isScanning = false;
                _scanStateService?.NotifyScanCancelled();
                return;
            }
            if (_currentJob.Status is "Error")
            {
                _state = "error";
                _error = "Error en el escaneo";
                _isScanning = false;
                _scanStateService?.NotifyScanCancelled();
                return;
            }

            // Start timer-based polling for ongoing jobs
            _pollingCts?.Dispose();
            _pollingCts = new CancellationTokenSource();
            _ = PollWithTimerAsync(_pollingCts.Token);
        }
        catch (Exception ex)
        {
            _error = $"Error al escanear: {ex.Message}";
            _state = "error";
            _isScanning = false;
            _scanStateService?.NotifyScanCancelled();
        }
    }

    private async Task CancelScanAsync()
    {
        if (_currentJob is null) return;

        try
        {
            await ScanClient.CancelScanAsync(_currentJob.Id);
            _state = "Cancelled";
            _isScanning = false;
            _pollingCts?.Cancel();
            _scanStateService?.NotifyScanCancelled();
        }
        catch (Exception ex)
        {
            _error = $"Error al cancelar: {ex.Message}";
        }
    }

    private async Task PollWithTimerAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                // If the job is no longer running, stop polling
                if (_currentJob?.Status is not ("running" or "paused"))
                    break;

                ScanJobDto? job;
                try
                {
                    job = await ScanClient.GetJobAsync(_currentJob.Id, ct);
                }
                catch (OperationCanceledException)
                {
                    throw; // let the outer catch handle it
                }
                catch (Exception ex)
                {
                    _error = $"Error consultando progreso: {ex.Message}";
                    _state = "error";
                    _isScanning = false;
                    await InvokeAsync(StateHasChanged);
                    break;
                }

                if (job is null) continue;

                _currentJob = job;
                _processed = job.ProcessedFiles;
                _total = job.TotalFiles;

                if (string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase))
                {
                    _state = "Completed";
                    _isScanning = false;
                    _scanStateService?.NotifyScanComplete();
                    await InvokeAsync(StateHasChanged);
                    break;
                }
                if (string.Equals(job.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    _state = "Cancelled";
                    _isScanning = false;
                    _scanStateService?.NotifyScanCancelled();
                    await InvokeAsync(StateHasChanged);
                    break;
                }
                if (string.Equals(job.Status, "error", StringComparison.OrdinalIgnoreCase))
                {
                    _state = "error";
                    _error = "Error en el escaneo";
                    _isScanning = false;
                    _scanStateService?.NotifyScanCancelled();
                    await InvokeAsync(StateHasChanged);
                    break;
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Timer was cancelled (e.g., Cancel button clicked)
            // State is already updated by CancelScanAsync or DisposeAsync
        }
    }
}
