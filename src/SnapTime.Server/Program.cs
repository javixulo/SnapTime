// [F0-US-006] [F0-US-008] [F1-US-005]
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
using SnapTime.Domain.Services;
using SnapTime.Infrastructure.Config;
using SnapTime.Infrastructure.Data;
using SnapTime.Infrastructure.Logging;
using SnapTime.Infrastructure.Services;
using SnapTime.Server.Models;

var builder = WebApplication.CreateBuilder(args);

var configService = new ConfigService("snaptime.config.json");
builder.Services.AddSingleton(configService);

Log.Logger = SerilogSetup.CreateConfiguration(configService.Current).CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddDbContext<SnapTimeDbContext>(options =>
{
    var connString = $"Data Source={configService.Current.Database.Path}";
    options.UseSqlite(connString);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7099", "http://localhost:5213", "http://localhost:5027")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => builder.Environment.IsDevelopment());
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddScoped<IDirectoryWalker, DirectoryWalker>();
builder.Services.AddScoped<IMetadataExtractor, MetadataExtractorService>();
builder.Services.AddScoped<IFileSystemMetadataExtractor, FileSystemMetadataExtractorService>();
builder.Services.AddSingleton<BackgroundJobRunner>();
builder.Services.AddSingleton<IBackgroundJobRunner>(sp => sp.GetRequiredService<BackgroundJobRunner>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundJobRunner>());
builder.Services.AddScoped<IHeuristic, H006FilenameHeuristic>();
builder.Services.AddScoped<IScanJobService, ScanJobService>();

var app = builder.Build();

// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SnapTimeDbContext>();
    db.Database.Migrate();
}

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", DateTime.UtcNow)));

// [F4-US-005] Browse filesystem directories
app.MapGet("/api/filesystem/directories", (string? path) =>
{
    try
    {
        string[] entries;

        if (string.IsNullOrEmpty(path))
        {
            // No path: return root directories
            entries = GetRootDirectories();
        }
        else
        {
            var resolved = Path.GetFullPath(path);

            if (!Directory.Exists(resolved))
                return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Directory does not exist." } });

            entries = GetFilteredDirectoryNames(resolved);
        }

        return Results.Ok(entries);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(403);
    }
    catch (PathTooLongException)
    {
        return Results.BadRequest(new { error = new { code = "PATH_TOO_LONG", message = "Path exceeds the system maximum length." } });
    }
    catch (DirectoryNotFoundException)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Directory does not exist." } });
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_PATH", message = "Path contains invalid characters." } });
    }
});

app.MapPost("/api/jobs", async (CreateJobRequest request, IScanJobService jobService) =>
{
    var validationError = ValidateCreateJobRequest(request);
    if (validationError is not null) return validationError;

    var rootPath = Path.GetFullPath(request.RootPath);

    try
    {
        var job = await jobService.CreateJobAsync(rootPath, request.IncludeSubfolders);
        return Results.Created($"/api/jobs/{job.Id}", ToDto(job));
    }
    catch (InvalidOperationException)
    {
        return Results.Conflict(new { error = new { code = "CONFLICT", message = "A job is already running for this path" } });
    }
});

app.MapGet("/api/jobs", async (IScanJobService jobService) =>
{
    var jobs = await jobService.GetAllJobsAsync();
    return Results.Ok(jobs.Select(ToDto));
});

app.MapGet("/api/jobs/{id:guid}", async (Guid id, IScanJobService jobService) =>
{
    var job = await jobService.GetJobAsync(id);
    return job is null ? Results.NotFound() : Results.Ok(ToDto(job));
});

app.MapPost("/api/jobs/{id:guid}/pause", async (Guid id, IScanJobService jobService) =>
    await HandleJobAction(id, jobService, () => jobService.PauseJobAsync(id)));

app.MapPost("/api/jobs/{id:guid}/resume", async (Guid id, IScanJobService jobService) =>
    await HandleJobAction(id, jobService, () => jobService.ResumeJobAsync(id)));

app.MapPost("/api/jobs/{id:guid}/cancel", async (Guid id, IScanJobService jobService) =>
{
    var job = await jobService.GetJobAsync(id);
    if (job is null) return Results.NotFound();
    await jobService.CancelJobAsync(id);
    var updatedJob = await jobService.GetJobAsync(id);
    return Results.Ok(ToDto(updatedJob));
});

// [F5] Photo grid — list files and directories with optional DB cross-reference
app.MapGet("/api/photos", async (
    SnapTimeDbContext db,
    string? path = null,
    int page = 1,
    int pageSize = 50) =>
{
    page = Math.Max(1, page);
    if (pageSize <= 0) pageSize = 50;
    pageSize = Math.Min(pageSize, 100);

    if (string.IsNullOrEmpty(path))
    {
        return Results.Ok(new PhotoGridResponse([], 0, page));
    }

    try
    {
        var resolved = Path.GetFullPath(path);

        // Reject system directories (consistency with POST /api/jobs)
        var resolvedDirInfo = new DirectoryInfo(resolved);
        if (HasSystemPathPrefix(resolved) || IsSystemDirectoryName(resolvedDirInfo.Name))
            return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Path is a system directory" } });

        var items = new List<PhotoGridItem>();

        if (Directory.Exists(resolved))
        {
            // List subdirectories (non-hidden, non-system)
            foreach (var dirPath in Directory.EnumerateDirectories(resolved))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dirPath);
                    if (!dirInfo.Attributes.HasFlag(FileAttributes.Hidden) &&
                        !dirInfo.Attributes.HasFlag(FileAttributes.System) &&
                        !IsSystemDirectoryName(dirInfo.Name) &&
                        !HasSystemPathPrefix(dirPath))
                    {
                        items.Add(new PhotoGridItem(
                            Guid.Empty,
                            dirInfo.Name,
                            dirPath,
                            IsDirectory: true,
                            ThumbnailUrl: null,
                            MediaStatus.Pending,
                            HasSuggestion: false,
                            SuggestedDate: null,
                            MediaType.Image));
                    }
                }
                catch (UnauthorizedAccessException) { }
            }

            // List supported media files
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
                ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"
            };

            var filePaths = Directory.EnumerateFiles(resolved)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            // Batch lookup: find all matching assets in DB
            var prefix = resolved.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var assets = await db.MediaAssets
                .Where(a => a.FilePath.StartsWith(prefix))
                .ToListAsync();

            var assetMap = assets.ToDictionary(a => a.FilePath, a => a);

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                var mediaType = ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm" or ".m4v"
                    ? MediaType.Video
                    : MediaType.Image;

                if (assetMap.TryGetValue(filePath, out var asset))
                {
                    var computedStatus = asset.SuggestedDate.HasValue
                        ? MediaStatus.HasSuggestion
                        : MediaStatus.NoSuggestion;
                    items.Add(new PhotoGridItem(
                        asset.Id,
                        fileName,
                        filePath,
                        IsDirectory: false,
                        $"/api/thumbnails/{asset.Id}",
                        computedStatus,
                        asset.SuggestedDate.HasValue,
                        asset.SuggestedDate,
                        asset.MediaType));
                }
                else
                {
                    items.Add(new PhotoGridItem(
                        Guid.Empty,
                        fileName,
                        filePath,
                        IsDirectory: false,
                        $"/api/thumbnails/from-file?path={Uri.EscapeDataString(filePath)}",
                        MediaStatus.Pending,
                        HasSuggestion: false,
                        SuggestedDate: null,
                        mediaType));
                }
            }
        }
        else
        {
            // Directory does not exist on filesystem — fallback to DB query
            var prefix = resolved.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var assets = await db.MediaAssets
                .Where(a => a.FilePath.StartsWith(prefix))
                .OrderBy(a => a.FileName)
                .ToListAsync();

            foreach (var asset in assets)
            {
                var computedStatus = asset.SuggestedDate.HasValue
                    ? MediaStatus.HasSuggestion
                    : MediaStatus.NoSuggestion;
                items.Add(new PhotoGridItem(
                    asset.Id,
                    asset.FileName,
                    asset.FilePath,
                    IsDirectory: false,
                    $"/api/thumbnails/{asset.Id}",
                    computedStatus,
                    asset.SuggestedDate.HasValue,
                    asset.SuggestedDate,
                    asset.MediaType));
            }
        }

        // Order: directories first (alphabetical), then files (alphabetical)
        items = items
            .OrderByDescending(i => i.IsDirectory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totalCount = items.Count;
        var pagedItems = items
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Results.Ok(new PhotoGridResponse(pagedItems, totalCount, page));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(403);
    }
    catch (PathTooLongException)
    {
        return Results.BadRequest(new { error = new { code = "PATH_TOO_LONG", message = "Path exceeds the system maximum length." } });
    }
    catch (DirectoryNotFoundException)
    {
        return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "Directory does not exist." } });
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_PATH", message = "Path contains invalid characters." } });
    }
})
.WithName("GetPhotos");

// [F6] Media asset detail — returns full detail with evidence and metadata
app.MapGet("/api/media-assets/{id:guid}", async (Guid id, SnapTimeDbContext db) =>
{
    var asset = await db.MediaAssets
        .Include(a => a.EvidenceEntries)
        .Include(a => a.MetadataEntries)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (asset is null)
        return Results.NotFound();

    var metadata = asset.MetadataEntries
        .ToDictionary(m => m.Tag, m => m.Value, StringComparer.OrdinalIgnoreCase);

    return Results.Ok(new MediaAssetDetailDto(
        asset.Id,
        asset.FilePath,
        asset.FileName,
        asset.MediaType,
        asset.FileSize,
        DateTimeOriginal: TryParseMetadataDate(metadata, "Exif SubIFD:Date/Time Original"),
        SubSecDateTimeOriginal: metadata.TryGetValue("Exif SubIFD:Sub-Sec Time Original", out var sub) ? sub : null,
        CreateDate: TryParseMetadataDate(metadata, "Exif IFD0:Date/Time Digitized")
                    ?? TryParseMetadataDate(metadata, "QuickTime Movie Header:Created"),
        ModifyDate: TryParseMetadataDate(metadata, "QuickTime Movie Header:Modified")
                    ?? TryParseMetadataDate(metadata, "Exif IFD0:Date/Time"),
        asset.FileCreatedAt,
        asset.FileModifiedAt,
        asset.ConfidenceScore,
        asset.SuggestedDate,
        asset.SuggestedByHeuristic,
        asset.EvidenceEntries.OrderByDescending(e => e.Weight).Select(e => new EvidenceDto(
            e.HeuristicId,
            e.HeuristicName,
            e.Weight,
            MapDirection(e.Direction),
            e.Description
        )).ToList()
    ));
})
.WithName("GetMediaAssetDetail");

// [F6] File metadata — read metadata directly from disk without DB
app.MapGet("/api/media-assets/from-file", async (string path, IMetadataExtractor metadataExtractor) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.BadRequest(new { error = new { code = "INVALID_PATH", message = "Path is required." } });

    try
    {
        var resolved = Path.GetFullPath(path);

        if (!System.IO.File.Exists(resolved))
            return Results.NotFound(new { error = new { code = "NOT_FOUND", message = "File does not exist." } });

        var fileInfo = new FileInfo(resolved);
        var ext = fileInfo.Extension.ToLowerInvariant();

        var videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v"
        };
        var mediaType = videoExtensions.Contains(ext) ? MediaType.Video : MediaType.Image;

        var metadataEntries = await metadataExtractor.ExtractAsync(resolved, mediaType, CancellationToken.None);
        var metadataDict = metadataEntries
            .ToDictionary(m => m.Tag, m => m.Value, StringComparer.OrdinalIgnoreCase);

        return Results.Ok(new FileMetadataDto(
            resolved,
            fileInfo.Name,
            fileInfo.Length,
            DateTimeOriginal: TryParseMetadataDate(metadataDict, "Exif SubIFD:Date/Time Original"),
            SubSecDateTimeOriginal: metadataDict.TryGetValue("Exif SubIFD:Sub-Sec Time Original", out var sub) ? sub : null,
            CreateDate: TryParseMetadataDate(metadataDict, "Exif IFD0:Date/Time Digitized")
                        ?? TryParseMetadataDate(metadataDict, "QuickTime Movie Header:Created"),
            ModifyDate: TryParseMetadataDate(metadataDict, "QuickTime Movie Header:Modified")
                        ?? TryParseMetadataDate(metadataDict, "Exif IFD0:Date/Time"),
            FileCreatedAt: fileInfo.CreationTime,
            FileModifiedAt: fileInfo.LastWriteTime
        ));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(403);
    }
    catch (PathTooLongException)
    {
        return Results.BadRequest(new { error = new { code = "PATH_TOO_LONG", message = "Path exceeds the system maximum length." } });
    }
    catch (ArgumentException)
    {
        return Results.BadRequest(new { error = new { code = "INVALID_PATH", message = "Path contains invalid characters." } });
    }
})
.WithName("GetFileMetadata");

// [F5] Thumbnail — serve actual file as image (full-size, no resize yet)
app.MapGet("/api/thumbnails/{assetId:guid}", async (Guid assetId, SnapTimeDbContext db) =>
{
    var asset = await db.MediaAssets.FindAsync(assetId);
    if (asset is null || !System.IO.File.Exists(asset.FilePath))
        return Results.NotFound();

    var ext = Path.GetExtension(asset.FilePath).ToLowerInvariant();
    var contentType = ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };

    var bytes = await System.IO.File.ReadAllBytesAsync(asset.FilePath);
    return Results.File(bytes, contentType);
})
.WithName("GetThumbnail");

app.MapGet("/api/thumbnails/placeholder", () =>
{
    var png = Convert.FromBase64String(PlaceholderBase64);
    return Results.File(png, "image/png");
});

// [F5] Serve thumbnail from any file path (no DB lookup needed)
app.MapGet("/api/thumbnails/from-file", (string path) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.Redirect("/api/thumbnails/placeholder");

    try
    {
        var resolved = Path.GetFullPath(path);
        if (!System.IO.File.Exists(resolved))
            return Results.Redirect("/api/thumbnails/placeholder");

        var ext = Path.GetExtension(resolved).ToLowerInvariant();

        // Video files cannot be rendered by <img> — redirect to placeholder
        var videoExtensions = new HashSet<string>
        {
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".m4v", ".3gp"
        };

        if (videoExtensions.Contains(ext))
            return Results.Redirect("/api/thumbnails/placeholder");

        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => null // unknown/unrecognized → placeholder
        };

        if (contentType is null)
            return Results.Redirect("/api/thumbnails/placeholder");

        var bytes = System.IO.File.ReadAllBytes(resolved);
        return Results.File(bytes, contentType);
    }
    catch
    {
        return Results.Redirect("/api/thumbnails/placeholder");
    }
});

// [F5] Stream video files with correct content type for <video> element
app.MapGet("/api/video/stream", (string path) =>
{
    if (string.IsNullOrWhiteSpace(path))
        return Results.NotFound();

    try
    {
        var resolved = Path.GetFullPath(path);
        if (!System.IO.File.Exists(resolved))
            return Results.NotFound();

        var ext = Path.GetExtension(resolved).ToLowerInvariant();
        var contentType = ext switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".m4v" => "video/mp4",
            ".3gp" => "video/3gpp",
            _ => "application/octet-stream"
        };

        var bytes = System.IO.File.ReadAllBytes(resolved);
        return Results.File(bytes, contentType);
    }
    catch
    {
        return Results.NotFound();
    }
})
.WithName("StreamVideo");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();

app.Run();

static IResult? ValidateCreateJobRequest(CreateJobRequest request)
{
    if (string.IsNullOrWhiteSpace(request.RootPath))
        return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "rootPath is required" } });

    string resolved;
    try
    {
        resolved = Path.GetFullPath(request.RootPath);
    }
    catch
    {
        return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "rootPath contains invalid characters" } });
    }

    if (!Directory.Exists(resolved))
        return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "rootPath does not exist" } });

    var dirInfo = new DirectoryInfo(resolved);
    if (HasSystemPathPrefix(resolved) || IsSystemDirectoryName(dirInfo.Name))
        return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "rootPath is a system directory and cannot be scanned" } });

    return null;
}

static async Task<IResult> HandleJobAction(Guid id, IScanJobService jobService, Func<Task> action)
{
    var job = await jobService.GetJobAsync(id);
    if (job is null) return Results.NotFound();
    await action();
    var updatedJob = await jobService.GetJobAsync(id);
    return Results.Ok(ToDto(updatedJob));
}

static JobDto ToDto(ScanJob job) => new(
    job.Id,
    job.Status,
    job.RootPath,
    job.IncludeSubfolders,
    job.TotalFiles,
    job.ProcessedFiles,
    job.ErrorCount,
    job.CreatedAt,
    job.CompletedAt
);

static string[] GetRootDirectories()
{
    if (OperatingSystem.IsWindows())
    {
        return DriveInfo.GetDrives()
            .Select(d => d.Name.TrimEnd('\\'))
            .ToArray();
    }

    // macOS / Linux: enumerate root "/" and return directory names only
    return GetFilteredDirectoryNames("/");
}

static string[] GetFilteredDirectoryNames(string path)
{
    var results = new List<string>();

    foreach (var entry in Directory.EnumerateDirectories(path))
    {
        try
        {
            var dirInfo = new DirectoryInfo(entry);

            // Skip hidden and system directories
            if (dirInfo.Attributes.HasFlag(FileAttributes.Hidden) ||
                dirInfo.Attributes.HasFlag(FileAttributes.System))
                continue;

            var name = dirInfo.Name;

            // Skip known system/reserved directory names
            if (IsSystemDirectoryName(name))
                continue;

            // Skip known system path prefixes (macOS / Linux)
            if (!OperatingSystem.IsWindows() && HasSystemPathPrefix(entry))
                continue;

            results.Add(name);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we cannot access
        }
    }

    return results.ToArray();
}

static bool IsSystemDirectoryName(string name)
{
    if (OperatingSystem.IsWindows())
    {
        return name switch
        {
            "WINDOWS" or "Program Files" or "Program Files (x86)" or "ProgramData"
                or "System Volume Information" or "$Recycle.Bin" or "Recovery"
                or "WindowsApps" or "Windows10Upgrade" or "WinSxS" => true,
            _ => false
        };
    }

    return name switch
    {
        "System" or "proc" or "sys" or "dev" or "cores" or "Volumes" => true,
        _ => false
    };
}

static bool HasSystemPathPrefix(string fullPath)
{
    return fullPath == "/System" || fullPath.StartsWith("/System/")
        || fullPath == "/proc" || fullPath.StartsWith("/proc/")
        || fullPath == "/sys" || fullPath.StartsWith("/sys/")
        || fullPath == "/dev" || fullPath.StartsWith("/dev/")
        || fullPath == "/private/var" || fullPath.StartsWith("/private/var/")
        || fullPath == "/cores" || fullPath.StartsWith("/cores/")
        || fullPath == "/Volumes" || fullPath.StartsWith("/Volumes/")
        || fullPath == "/private/etc" || fullPath.StartsWith("/private/etc/")
        || fullPath == "/private/tmp" || fullPath.StartsWith("/private/tmp/");
}



public partial class Program
{
    private const string PlaceholderBase64 = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAJ0lEQVR4nO3NMQ0AAAwDoPpXVllVsWMJGCA9FoFAIBAIBAKBQPAlGGDXYIj+Um+RAAAAAElFTkSuQmCC";

    private static readonly string[] MetadataDateFormats =
    [
        "yyyy:MM:dd HH:mm:ss",
        "yyyy:MM:dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd"
    ];

    /// <summary>
    /// Maps the EvidenceDirection enum to a lowercase string for the API contract.
    /// </summary>
    internal static string MapDirection(EvidenceDirection direction) => direction switch
    {
        EvidenceDirection.Positive => "positive",
        EvidenceDirection.Negative => "negative",
        EvidenceDirection.Correction => "correction",
        _ => "positive"
    };

    /// <summary>
    /// Attempts to parse a date value from the metadata dictionary by tag key.
    /// Handles both EXIF colon-separated dates and standard ISO formats.
    /// Returns null if the tag is missing or unparseable.
    /// </summary>
    internal static DateTime? TryParseMetadataDate(Dictionary<string, string?> metadata, string tag)
    {
        if (!metadata.TryGetValue(tag, out var value) || value is null)
            return null;

        if (DateTime.TryParseExact(value, MetadataDateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;

        return null;
    }
}

/// <summary>
/// Response from the health check endpoint.
/// </summary>
/// <param name="Status">Service status string (e.g. "ok").</param>
/// <param name="Timestamp">UTC timestamp of the check.</param>
public record HealthResponse(string Status, DateTime Timestamp);
