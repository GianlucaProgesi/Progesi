using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.SQLite;

namespace Progesi.Data.EF
{
  [DbConfigurationType(typeof(Progesi.Data.EF.ProgesiDbConfiguration))]
  public class ProgesiDbContext : DbContext
  {
    public ProgesiDbContext(string dbPath)
      : base(new SQLiteConnection($"Data Source={dbPath};Foreign Keys=True;"), true) { }

    public ProgesiDbContext(SQLiteConnection cn, bool owns) : base(cn, owns) { }

    public DbSet<MetadataRow> Metadata { get; set; }
    public DbSet<RefRow> Refs { get; set; }
    public DbSet<VariableRow> Variables { get; set; }
    public DbSet<VariableDepend> VariableDepends { get; set; }

    protected override void OnModelCreating(DbModelBuilder mb)
    {
      // Metadata
      mb.Entity<MetadataRow>().ToTable("Metadata")
        .HasKey(m => m.Id);
      mb.Entity<MetadataRow>()
        .Property(m => m.Hash).IsRequired();

      // Refs
      mb.Entity<RefRow>().ToTable("Refs")
        .HasKey(r => new { r.MetaId, r.Ref });
      mb.Entity<RefRow>()
        .HasRequired(r => r.Metadata)
        .WithMany(m => m.Refs)
        .HasForeignKey(r => r.MetaId)
        .WillCascadeOnDelete(true);

      // Variables
      mb.Entity<VariableRow>().ToTable("Variables")
        .HasKey(v => v.Id);
      mb.Entity<VariableRow>()
        .Property(v => v.Hash).IsRequired();
      mb.Entity<VariableRow>()
        .Property(v => v.Name).IsRequired();
      mb.Entity<VariableRow>()
        .HasOptional(v => v.Metadata)
        .WithMany(m => m.Variables)
        .HasForeignKey(v => v.MetaId)
        .WillCascadeOnDelete(false);

      // VariableDepends
      mb.Entity<VariableDepend>().ToTable("VariableDepends")
        .HasKey(d => new { d.VarId, d.DepId });
      mb.Entity<VariableDepend>()
        .HasRequired(d => d.Variable)
        .WithMany(v => v.Depends)
        .HasForeignKey(d => d.VarId)
        .WillCascadeOnDelete(true);

      base.OnModelCreating(mb);
    }
  }

  public class MetadataRow
  {
    [Key] public int Id { get; set; }
    [Required] public string Hash { get; set; }
    public string By { get; set; }
    public string Description { get; set; }
    public string LM { get; set; }

    public virtual ICollection<RefRow> Refs { get; set; }
    public virtual ICollection<VariableRow> Variables { get; set; }
  }

  public class RefRow
  {
    [Key, Column(Order = 0)] public int MetaId { get; set; }
    [Key, Column(Order = 1)] public string Ref { get; set; }

    [ForeignKey(nameof(MetaId))] public virtual MetadataRow Metadata { get; set; }
  }

  public class VariableRow
  {
    [Key] public int Id { get; set; }
    [Required] public string Hash { get; set; }
    [Required] public string Name { get; set; }
    public string Value { get; set; }
    public string ValC { get; set; }

    public int? MetaId { get; set; }
    public bool Assumption { get; set; }

    [ForeignKey(nameof(MetaId))] public virtual MetadataRow Metadata { get; set; }
    public virtual ICollection<VariableDepend> Depends { get; set; }
  }

  public class VariableDepend
  {
    [Key, Column(Order = 0)] public int VarId { get; set; }
    [Key, Column(Order = 1)] public int DepId { get; set; }

    [ForeignKey(nameof(VarId))] public virtual VariableRow Variable { get; set; }
  }
}
