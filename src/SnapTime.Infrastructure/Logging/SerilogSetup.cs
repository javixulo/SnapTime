// [F0-US-008]
using Serilog;
using Serilog.Events;
using SnapTime.Domain.Config;

namespace SnapTime.Infrastructure.Logging;

public static class SerilogSetup
{
    public static LoggerConfiguration CreateConfiguration(SnapTimeConfig config)
    {
        var level = config.Logging.Level.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "verbose" => LogEventLevel.Verbose,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };

        var cfg = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        if (config.Logging.File is { Length: > 0 } filePath)
        {
            cfg = cfg.WriteTo.File(filePath, rollingInterval: RollingInterval.Day);
        }

        return cfg;
    }
}
