// [F4-US-000] ScanPanel code-behind
using Microsoft.AspNetCore.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Components;

public partial class ScanPanel : IAsyncDisposable
{
    private const string DefaultScanPath = "sample/";
    private readonly CancellationTokenSource _cts = new();

    [Inject]
    private IScanClient ScanClient { get; set; } = null!;

    private string _rootPath = string.Empty;
    private bool _isScanning;
    private ScanJobDto? _currentJob;
    private string? _error;

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task StartScanAsync()
    {
        _error = null;
        _currentJob = null;
        _isScanning = true;

        try
        {
            var path = ResolveScanPath();
            _currentJob = await ScanClient.StartScanAsync(path);
            if (_currentJob?.Status is "Completed" or "Error" or "Cancelled")
                return;
            await PollJobProgressAsync();
        }
        catch (Exception ex)
        {
            _error = $"Error al escanear: {ex.Message}";
        }
        finally
        {
            _isScanning = false;
        }
    }

    private string ResolveScanPath()
    {
        return string.IsNullOrWhiteSpace(_rootPath)
            ? DefaultScanPath
            : _rootPath;
    }

    private async Task PollJobProgressAsync()
    {
        while (!_cts.Token.IsCancellationRequested &&
               _currentJob is not null &&
               _currentJob.Status is "Running" or "Paused")
        {
            await Task.Delay(500, _cts.Token);
            _currentJob = await ScanClient.GetJobAsync(_currentJob.Id, _cts.Token);
            StateHasChanged();
        }
    }
}
