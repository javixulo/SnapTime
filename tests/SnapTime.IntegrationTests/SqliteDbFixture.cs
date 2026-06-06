using Microsoft.EntityFrameworkCore;
using SnapTime.Infrastructure.Data;

// [F2-US-001]
namespace SnapTime.IntegrationTests;

[CollectionDefinition("SqliteIntegration")]
public class SqliteIntegrationCollection : ICollectionFixture<SqliteDbFixture> { }

public class SqliteDbFixture : IDisposable
{
    private static readonly string DbPath = Path.Combine(
        Path.GetTempPath(), $"snaptime-test-{Guid.NewGuid()}.db");

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

    public void ResetDatabase()
    {
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try { File.Delete(DbPath); } catch { }
    }
}
