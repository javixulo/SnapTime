// [F8-US-003] IApplyService — orchestrates applying approved suggestions to media files
using SnapTime.Domain.Models;

namespace SnapTime.Domain.Interfaces;

public interface IApplyService
{
    Task<ApplyChangesResponse> ApplyAsync(ApplyChangesRequest request, CancellationToken ct = default);
}
