// [F6] Evidence entry DTO for media asset detail
namespace SnapTime.Client.Models;

public class EvidenceDto
{
    public string HeuristicId { get; set; } = string.Empty;
    public string HeuristicName { get; set; } = string.Empty;
    public double Weight { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
