// [F6] Evidence entry DTO for media asset detail
namespace SnapTime.Server.Models;

public record EvidenceDto(
    string HeuristicId,
    string HeuristicName,
    double Weight,
    string Direction,
    string Description
);
