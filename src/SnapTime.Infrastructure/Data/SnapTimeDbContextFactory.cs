// [F0-US-004]
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SnapTime.Infrastructure.Data;

public class SnapTimeDbContextFactory : IDesignTimeDbContextFactory<SnapTimeDbContext>
{
    public SnapTimeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SnapTimeDbContext>();
        optionsBuilder.UseSqlite("Data Source=SnapTime.db");
        return new SnapTimeDbContext(optionsBuilder.Options);
    }
}
