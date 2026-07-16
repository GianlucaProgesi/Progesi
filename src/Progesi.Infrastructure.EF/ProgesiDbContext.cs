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
      entity.Property(e => e.ContentHash).IsRequired();
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
  }
}
