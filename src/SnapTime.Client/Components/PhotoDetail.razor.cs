// [F6][F7] PhotoDetail code-behind — carga de detalle, suscripción a ScanStateService, botones Aceptar/Rechazar
using Microsoft.AspNetCore.Components;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Components;

public partial class PhotoDetail : IDisposable
{
    private IScanStateService? _scanStateService;
    private IReviewClient? _reviewClient;
    private MediaAssetDetailDto? _detail;
    private FileMetadataDto? _fileMetadata;
    private bool _isLoading;
    private string? _error;
    private bool _canAccept;

    [Inject]
    private IPhotoClient PhotoClient { get; set; } = null!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = null!;

    [Parameter]
    public Guid? SelectedAssetId { get; set; }

    [Parameter]
    public string? SelectedAssetPath { get; set; }

    [Parameter]
    public EventCallback OnAccept { get; set; }

    [Parameter]
    public EventCallback OnReject { get; set; }

    protected override void OnInitialized()
    {
        _scanStateService = ServiceProvider.GetService<IScanStateService>();
        _reviewClient = ServiceProvider.GetService<IReviewClient>();

        if (_scanStateService is not null)
        {
            _scanStateService.StateChanged += OnScanStateChanged;
            _scanStateService.ApplyCompleted += OnApplyCompleted;
        }
    }

    public void Dispose()
    {
        if (_scanStateService is not null)
        {
            _scanStateService.StateChanged -= OnScanStateChanged;
            _scanStateService.ApplyCompleted -= OnApplyCompleted;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (SelectedAssetId.HasValue)
        {
            _isLoading = true;
            _error = null;
            _detail = null;
            _fileMetadata = null;

            try
            {
                _detail = await PhotoClient.GetAssetDetailAsync(SelectedAssetId.Value);
                UpdateCanAccept();
            }
            catch (Exception ex)
            {
                _error = $"Error al cargar detalle: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
            }
        }
        else if (!string.IsNullOrEmpty(SelectedAssetPath))
        {
            _isLoading = true;
            _error = null;
            _detail = null;
            _fileMetadata = null;

            try
            {
                _fileMetadata = await PhotoClient.GetFileMetadataAsync(SelectedAssetPath);
                if (_fileMetadata is null)
                    _error = "No se pudieron obtener los metadatos del archivo.";
            }
            catch (Exception ex)
            {
                _error = $"Error al cargar metadatos: {ex.Message}";
            }
            finally
            {
                _isLoading = false;
            }
        }
        else
        {
            _detail = null;
            _fileMetadata = null;
            _isLoading = false;
            _error = null;
        }
    }

    private async void OnScanStateChanged()
    {
        UpdateCanAccept();
        await InvokeAsync(StateHasChanged);
    }

    private async void OnApplyCompleted()
    {
        // Reload detail after apply so metadata, status and dates reflect changes
        if (SelectedAssetId.HasValue)
        {
            try
            {
                _detail = await PhotoClient.GetAssetDetailAsync(SelectedAssetId.Value);
                UpdateCanAccept();
            }
            catch
            {
                // Keep stale data if reload fails
            }
        }
        await InvokeAsync(StateHasChanged);
    }

    private void UpdateCanAccept()
    {
        var notScanning = _scanStateService?.IsScanning == false;
        _canAccept = notScanning && _detail?.SuggestedDate is not null;
    }

    private async Task HandleAcceptAsync()
    {
        if (!_canAccept || _detail is null) return;

        try
        {
            var result = await _reviewClient!.SingleReviewAsync(_detail.Id, "approved");
            _detail.SuggestionReviewStatus = result.SuggestionReviewStatus;
            _canAccept = false;
            await OnAccept.InvokeAsync();
        }
        catch (Exception ex)
        {
            _error = $"Error al aceptar sugerencia: {ex.Message}";
        }
    }

    private async Task HandleRejectAsync()
    {
        if (!_canAccept || _detail is null) return;

        try
        {
            var result = await _reviewClient!.SingleReviewAsync(_detail.Id, "rejected");
            _detail.SuggestionReviewStatus = result.SuggestionReviewStatus;
            _canAccept = false;
            await OnReject.InvokeAsync();
        }
        catch (Exception ex)
        {
            _error = $"Error al rechazar sugerencia: {ex.Message}";
        }
    }

    private string GetConfidenceClass()
    {
        if (_detail is null) return "";
        if (_detail.ConfidenceScore >= 80) return "confidence-high";
        if (_detail.ConfidenceScore >= 50) return "confidence-medium";
        return "confidence-low";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private string GetAcceptClass()
    {
        return _canAccept ? "btn-accept" : "btn-accept disabled";
    }

    private string GetRejectClass()
    {
        return _canAccept ? "btn-reject" : "btn-reject disabled";
    }

    private static string GetDirectionClass(string direction)
    {
        return direction switch
        {
            "positive" => "direction-positive",
            "negative" => "direction-negative",
            "neutral" => "direction-neutral",
            "correction" => "direction-correction",
            _ => ""
        };
    }

    private string GetReviewStatusClass()
    {
        if (_detail?.SuggestionReviewStatus is null) return "status-unreviewed";
        return _detail.SuggestionReviewStatus switch
        {
            "Approved" or "approved" => "status-approved",
            "Rejected" or "rejected" => "status-rejected",
            _ => "status-unreviewed"
        };
    }

    private string GetReviewStatusLabel()
    {
        if (_detail?.SuggestionReviewStatus is null) return "Sin revisar";
        return _detail.SuggestionReviewStatus switch
        {
            "Approved" or "approved" => "✓ Aprobada",
            "Rejected" or "rejected" => "✗ Rechazada",
            _ => _detail.SuggestionReviewStatus
        };
    }
}
