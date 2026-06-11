// [F7-US-003][F7-US-004] BatchActions code-behind — botones de lote, modal de confirmación, llamadas API
using Microsoft.AspNetCore.Components;
using SnapTime.Client.Services;

namespace SnapTime.Client.Components;

public partial class BatchActions : IDisposable
{
    private IScanStateService? _scanStateService;
    private IReviewClient? _reviewClient;
    private bool _showConfirmModal;
    private string? _errorMessage;
    private string _pendingStatus = "";
    private string _pendingScope = "";

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Parameter]
    public bool HasRecommendations { get; set; }

    [Parameter]
    public int RecommendationCount { get; set; }

    [Parameter]
    public string? CurrentFolderPath { get; set; }

    [Parameter]
    public EventCallback OnBatchReviewCompleted { get; set; }

    private bool IsDisabled => (_scanStateService?.IsScanning ?? false) || !HasRecommendations;

    protected override void OnInitialized()
    {
        _scanStateService = ServiceProvider.GetService<IScanStateService>();
        _reviewClient = ServiceProvider.GetService<IReviewClient>();

        if (_scanStateService is not null)
        {
            _scanStateService.StateChanged += OnScanStateChanged;
        }
    }

    public void Dispose()
    {
        if (_scanStateService is not null)
        {
            _scanStateService.StateChanged -= OnScanStateChanged;
        }
    }

    private void OnScanStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OpenModal(string scope, string status)
    {
        _pendingScope = scope;
        _pendingStatus = status;
        _showConfirmModal = true;
    }

    private void OpenModalAcceptFolder() => OpenModal("folder", "approved");
    private void OpenModalRejectFolder() => OpenModal("folder", "rejected");
    private void OpenModalAcceptTotal() => OpenModal("total", "approved");
    private void OpenModalRejectTotal() => OpenModal("total", "rejected");

    private async Task ConfirmAsync()
    {
        _showConfirmModal = false;

        if (_reviewClient is null) return;

        try
        {
            var rootPath = _pendingScope == "folder" ? CurrentFolderPath : null;
            _errorMessage = null;
            await _reviewClient.BatchReviewAsync(_pendingScope, _pendingStatus, rootPath);
            await OnBatchReviewCompleted.InvokeAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error al procesar la operación: {ex.Message}";
        }

        _pendingScope = "";
        _pendingStatus = "";
    }

    private void CancelModal()
    {
        _showConfirmModal = false;
        _errorMessage = null;
        _pendingScope = "";
        _pendingStatus = "";
    }

    private string AcceptAllClass => "btn-accept-all" + (IsDisabled ? " disabled" : "");
    private string RejectAllClass => "btn-reject-all" + (IsDisabled ? " disabled" : "");
    private string AcceptTotalClass => "btn-accept-total" + (IsDisabled ? " disabled" : "");
    private string RejectTotalClass => "btn-reject-total" + (IsDisabled ? " disabled" : "");

    private string GetConfirmText()
    {
        var action = _pendingStatus == "approved" ? "Se aprobarán " : "Se rechazarán ";
        return $"{action}{RecommendationCount} sugerencias";
    }
}
