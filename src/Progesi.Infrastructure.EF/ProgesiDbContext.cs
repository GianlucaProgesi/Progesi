using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF.Entities;

namespace Progesi.Infrastructure.EF;

public sealed class ProgesiDbContext : DbContext
{
  public ProgesiDbContext(DbContextOptions<ProgesiDbContext> options)
      : base(options)
  {
  }

  public DbSet<VariableEntity> Variables => Set<VariableEntity>();
  public DbSet<MetadataEntity> Metadata => Set<MetadataEntity>();
  public DbSet<ClusterEntity> Clusters => Set<ClusterEntity>();
  public DbSet<AxisEntity> Axis => Set<AxisEntity>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.Entity<VariableEntity>(entity =>
    {
      entity.ToTable("Variables");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).IsRequired();
      entity.Property(e => e.ValueType).IsRequired();
      entity.Property(e => e.Value).IsRequired();
      entity.Property(e => e.DependsJson).IsRequired();
      entity.Property(e => e.MetadataIdsJson).IsRequired();
      entity.Property(e => e.ContentHash).IsRequired();
      entity.Property(e => e.ObjectType).HasDefaultValue(string.Empty);
      entity.Property(e => e.ObjectPayloadJson).HasDefaultValue(string.Empty);
      entity.HasIndex(e => e.ContentHash).IsUnique();
    });

    modelBuilder.Entity<MetadataEntity>(entity =>
    {
      entity.ToTable("Metadata");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Json).IsRequired();
      entity.Property(e => e.LastModified).IsRequired();
      entity.Property(e => e.ContentHash).IsRequired();
      entity.HasIndex(e => e.ContentHash).IsUnique();
    });

    modelBuilder.Entity<ClusterEntity>(entity =>
    {
      entity.ToTable("Clusters");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Name).IsRequired();
      entity.Property(e => e.Description).HasDefaultValue(string.Empty);
      entity.Property(e => e.VariableIdsJson).IsRequired();
      entity.Property(e => e.ContentHash).IsRequired();
      entity.Property(e => e.Hashtag).IsRequired();
      entity.HasIndex(e => e.ContentHash).IsUnique();
      entity.HasIndex(e => e.Hashtag);
    });

    modelBuilder.Entity<AxisEntity>(entity =>
    {
      entity.ToTable("Axis");
      entity.HasKey(e => e.Id);
      entity.Property(e => e.AxisName).IsRequired();
      entity.Property(e => e.Name).IsRequired();
      entity.Property(e => e.ValueTypeKey).IsRequired();
      entity.Property(e => e.CurvePayload).HasDefaultValue(string.Empty);
      entity.Property(e => e.KeyPointsJson).IsRequired();
      entity.Property(e => e.FunctionPayload).HasDefaultValue(string.Empty);
      entity.Property(e => e.StationsJson).IsRequired();
      entity.Property(e => e.ContentHash).IsRequired();
      entity.Property(e => e.Hashtag).IsRequired();
      entity.HasIndex(e => e.ContentHash).IsUnique();
      entity.HasIndex(e => e.Hashtag);
    });
  }
}
