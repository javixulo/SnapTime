using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
        context.Database.EnsureCreated();
    }

    public string ConnectionString => $"Data Source={DbPath}";

    public DbContextOptions<SnapTimeDbContext> Options => new DbContextOptionsBuilder<SnapTimeDbContext>()
        .UseSqlite(ConnectionString)
        .Options;

    public SnapTimeDbContext CreateContext() => new(Options);

    public HttpClient CreateClient()
    {
        _factory = new WebApplicationFactory<Program>();
        return _factory.CreateClient();
    }

    public void ResetDatabase()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        try { File.Delete(DbPath); } catch { }
    }
}
