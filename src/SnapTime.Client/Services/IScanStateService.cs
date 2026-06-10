// [F7-US-003] ScanStateService interface — estado singleton de escaneo
namespace SnapTime.Client.Services;

public interface IScanStateService
{
    bool IsScanning { get; }
    bool HasCompletedScan { get; }
    event Action? StateChanged;
    void NotifyScanStart();
    void NotifyScanComplete();
    void NotifyScanCancelled();
    void Reset();
}
