using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SnapTime.Infrastructure.Data;

// [F2-US-001] [F4-US-005]
namespace SnapTime.IntegrationTests;

[CollectionDefinition("SqliteIntegration")]
public class SqliteIntegrationCollection : ICollectionFixture<SqliteDbFixture> { }

public class SqliteDbFixture : IDisposable
{
    private static readonly string DbPath = Path.Combine(
        Path.GetTempPath(), $"snaptime-test-{Guid.NewGuid()}.db");

    private WebApplicationFactory<Program>? _factory;

    public SqliteDbFixture()
    {
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public string ConnectionString => $"Data Source={DbPath}";

    public DbContextOptions<SnapTimeDbContext> Options => new DbContextOptionsBuilder<SnapTimeDbContext>()
        .UseSqlite(ConnectionString)
        .Options;

    public SnapTimeDbContext CreateContext() => new(Options);

    public HttpClient CreateClient()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove the existing DbContext options registration
                    // so the app uses the fixture's test database
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<SnapTimeDbContext>));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    // Register DbContext to use the fixture's test database
                    services.AddDbContext<SnapTimeDbContext>(options =>
                        options.UseSqlite(ConnectionString));
                });
            });

        return _factory.CreateClient();
    }

    public void ResetDatabase()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.Migrate();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        try { File.Delete(DbPath); } catch { }
    }
}
