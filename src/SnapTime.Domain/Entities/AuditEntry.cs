// [F0-US-003]
namespace SnapTime.Domain.Entities;

public class AuditEntry
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }
}
