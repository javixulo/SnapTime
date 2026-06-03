using Microsoft.Extensions.Hosting;
using SnapTime.Domain.Entities;

namespace SnapTime.Domain.Interfaces;

public interface IBackgroundJobRunner : IHostedService
{
    ValueTask EnqueueJobAsync(ScanJob job);
}
