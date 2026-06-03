using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;

namespace SnapTime.Infrastructure.Services;

public class BackgroundJobRunner : IBackgroundJobRunner
{
    private readonly Channel<ScanJob> _channel = Channel.CreateUnbounded<ScanJob>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobRunner> _logger;

    public BackgroundJobRunner(IServiceScopeFactory scopeFactory, ILogger<BackgroundJobRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async ValueTask EnqueueJobAsync(ScanJob job)
    {
        await _channel.Writer.WriteAsync(job);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background job runner started");

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scanJobService = scope.ServiceProvider.GetRequiredService<IScanJobService>();
                        await scanJobService.ProcessJobAsync(job.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing job {JobId}", job.Id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Background job runner is stopping");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in background job runner");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background job runner stopped");
        _channel.Writer.TryComplete();
        return Task.CompletedTask;
    }
}
