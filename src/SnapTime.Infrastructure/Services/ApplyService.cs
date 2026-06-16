// [F8-US-003] ApplyService — orchestrates applying approved suggestions
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Data;
using SnapTime.Domain.Models;

namespace SnapTime.Infrastructure.Services;

public class ApplyService : IApplyService
{
    private readonly SnapTimeDbContext _db;
    private readonly IExifWriter _exifWriter;

    public ApplyService(SnapTimeDbContext db, IExifWriter exifWriter)
    {
        _db = db;
        _exifWriter = exifWriter;
    }

    public async Task<ApplyChangesResponse> ApplyAsync(ApplyChangesRequest request, CancellationToken ct = default)
    {
        // Check for running scan job
        var hasRunningJob = await _db.ScanJobs.AnyAsync(j => j.Status == JobStatus.Running, ct);
        if (hasRunningJob)
            throw new InvalidOperationException("A scan job is currently running. Please wait for it to complete before applying changes.");

        var results = new List<ApplyResult>(request.MediaAssetIds.Count);
        var appliedCount = 0;

        foreach (var id in request.MediaAssetIds)
        {
            ct.ThrowIfCancellationRequested();

            var asset = await _db.MediaAssets
                .Include(a => a.MetadataEntries)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (asset is null)
            {
                results.Add(new ApplyResult(id, string.Empty, false, "NotFound"));
                continue;
            }

            if (asset.SuggestionStatus != SuggestionReviewStatus.Approved)
            {
                results.Add(new ApplyResult(id, asset.FileName, false, "NotApproved"));
                continue;
            }

            if (asset.SuggestedDate is null)
            {
                results.Add(new ApplyResult(id, asset.FileName, false, "NoSuggestedDate"));
                continue;
            }

            // Get original date from metadata
            DateTime? originalDate = asset.MetadataEntries?
                .FirstOrDefault(m => m.Tag is "Exif SubIFD:Date/Time Original" or "Exif IFD0:Date/Time" or "QuickTime Movie Header:Created")
                ?.Value is string dateStr && DateTime.TryParse(dateStr, out var parsed)
                ? parsed
                : null;

            // Get heuristic IDs
            var heuristicIds = !string.IsNullOrEmpty(asset.SuggestedByHeuristic)
                ? asset.SuggestedByHeuristic.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
                : new List<string>();

            try
            {
                var writeResult = await _exifWriter.WriteAsync(
                    asset.FilePath,
                    asset.MediaType,
                    asset.SuggestedDate.Value,
                    originalDate,
                    heuristicIds,
                    ct);

                if (writeResult.Success)
                {
                    asset.Status = MediaStatus.Completed;
                    results.Add(new ApplyResult(id, asset.FileName, true, null));
                    appliedCount++;
                }
                else
                {
                    results.Add(new ApplyResult(id, asset.FileName, false, writeResult.ErrorMessage));
                }
            }
            catch (Exception ex)
            {
                results.Add(new ApplyResult(id, asset.FileName, false, ex.Message));
            }
        }

        // Create audit entry
        var audit = new AuditEntry
        {
            Id = Guid.NewGuid(),
            EventType = "ApplyChanges",
            Payload = JsonSerializer.Serialize(new
            {
                requestedIds = request.MediaAssetIds,
                appliedCount,
                failedCount = results.Count - appliedCount,
                results = results.Select(r => new { r.MediaAssetId, r.Success, r.Error })
            }),
            CreatedAt = DateTime.UtcNow
        };
        _db.AuditEntries.Add(audit);

        await _db.SaveChangesAsync(ct);

        return new ApplyChangesResponse(results, appliedCount, results.Count - appliedCount, DateTime.UtcNow);
    }
}
