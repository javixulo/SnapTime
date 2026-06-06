using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SnapTime.Domain.Entities;
using SnapTime.Domain.Interfaces;
using SnapTime.Infrastructure.Services;

// [F1-US-010] [F2-US-003]
namespace SnapTime.IntegrationTests;

[Collection("SqliteIntegration")]
public class BackgroundJobRunnerIntegrationTests
{
    [Fact]
    public async Task StartAsync_EnqueuedJob_CallsProcessJobAsyncOnScanJobService()
    {
        var scanJobService = Substitute.For<IScanJobService>();
        var logger = Substitute.For<ILogger<BackgroundJobRunner>>();

        var services = new ServiceCollection();
        services.AddSingleton<IScanJobService>(scanJobService);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var runner = new BackgroundJobRunner(scopeFactory, logger);

        var job = new ScanJob { Id = Guid.NewGuid(), RootPath = "/test" };

        using var cts = new CancellationTokenSource();
        await runner.StartAsync(cts.Token);

        await runner.EnqueueJobAsync(job);

        await Task.Delay(500);

        await scanJobService.Received(1).ProcessJobAsync(job.Id);

        await runner.StopAsync(CancellationToken.None);
    }
}
