// [F0-US-006] [F0-US-008]
using Microsoft.EntityFrameworkCore;
using Serilog;
using SnapTime.Infrastructure.Config;
using SnapTime.Infrastructure.Data;
using SnapTime.Infrastructure.Logging;

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

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok", DateTime.UtcNow)));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();

app.Run();

public partial class Program { }

public record HealthResponse(string Status, DateTime Timestamp);
