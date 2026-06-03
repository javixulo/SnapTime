// [F0-US-004]
using Microsoft.EntityFrameworkCore;
using SnapTime.Domain.Entities;

namespace SnapTime.Infrastructure.Data;

public class SnapTimeDbContext : DbContext
{
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<MetadataEntry> MetadataEntries => Set<MetadataEntry>();
    public DbSet<EvidenceEntry> EvidenceEntries => Set<EvidenceEntry>();
    public DbSet<ScanJob> ScanJobs => Set<ScanJob>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public SnapTimeDbContext(DbContextOptions<SnapTimeDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MediaAsset>(entity =>
        {
            entity.HasIndex(e => e.FilePath).IsUnique();
            entity.HasIndex(e => e.ScanJobId);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.MediaType).HasConversion<string>();
        });

        modelBuilder.Entity<MetadataEntry>(entity =>
        {
            entity.HasIndex(e => e.MediaAssetId);
            entity.HasOne(e => e.MediaAsset)
                  .WithMany(m => m.MetadataEntries)
                  .HasForeignKey(e => e.MediaAssetId);
        });

        modelBuilder.Entity<EvidenceEntry>(entity =>
        {
            entity.HasIndex(e => e.MediaAssetId);
            entity.HasOne(e => e.MediaAsset)
                  .WithMany(m => m.EvidenceEntries)
                  .HasForeignKey(e => e.MediaAssetId);
        });

        modelBuilder.Entity<ScanJob>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.EventType);
        });
    }
}
