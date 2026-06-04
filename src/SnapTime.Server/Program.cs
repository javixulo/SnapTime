// [F0-US-006] [F0-US-008] [F1-US-005]
using Microsoft.EntityFrameworkCore;
using Serilog;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Enums;
using SnapTime.Domain.Interfaces;
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
        policy.WithOrigins("https://localhost:7099", "http://localhost:5213")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IDirectoryWalker, DirectoryWalker>();
builder.Services.AddScoped<IMetadataExtractor, MetadataExtractorService>();
builder.Services.AddScoped<IFileSystemMetadataExtractor, FileSystemMetadataExtractorService>();
builder.Services.AddSingleton<BackgroundJobRunner>();
builder.Services.AddSingleton<IBackgroundJobRunner>(sp => sp.GetRequiredService<BackgroundJobRunner>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundJobRunner>());
builder.Services.AddScoped<IScanJobService, ScanJobService>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", DateTime.UtcNow)));

app.MapPost("/api/jobs", async (CreateJobRequest request, IScanJobService jobService) =>
{
    var validationError = ValidateCreateJobRequest(request);
    if (validationError is not null) return validationError;

    var rootPath = Path.GetFullPath(request.RootPath);

    try
    {
        var job = await jobService.CreateJobAsync(rootPath);
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
    if (!Directory.Exists(request.RootPath))
        return Results.BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "rootPath does not exist" } });
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
    job.TotalFiles,
    job.ProcessedFiles,
    job.ErrorCount,
    job.CreatedAt,
    job.CompletedAt
);

public partial class Program { }

/// <summary>
/// Response from the health check endpoint.
/// </summary>
/// <param name="Status">Service status string (e.g. "ok").</param>
/// <param name="Timestamp">UTC timestamp of the check.</param>
public record HealthResponse(string Status, DateTime Timestamp);
