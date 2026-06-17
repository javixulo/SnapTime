// [F7-US-003] ScanStateService — singleton state coordinator for scan lifecycle
namespace SnapTime.Client.Services;

public class ScanStateService : IScanStateService
{
    public bool IsScanning { get; private set; }
    public bool HasCompletedScan { get; private set; }
    public event Action? StateChanged;
    public event Action? ApplyCompleted;

    public void NotifyScanStart()
    {
        IsScanning = true;
        StateChanged?.Invoke();
    }

    public void NotifyScanComplete()
    {
        IsScanning = false;
        HasCompletedScan = true;
        StateChanged?.Invoke();
    }

    public void NotifyScanCancelled()
    {
        IsScanning = false;
        StateChanged?.Invoke();
    }

    public void NotifyApplyCompleted()
    {
        ApplyCompleted?.Invoke();
    }

    public void Reset()
    {
        IsScanning = false;
        HasCompletedScan = false;
        StateChanged?.Invoke();
    }
}
