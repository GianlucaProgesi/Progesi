namespace Progesi.Infrastructure.EF.Entities;

public sealed class MetadataEntity
{
  public int Id { get; set; }
  public string Json { get; set; } = string.Empty;
  public string LastModified { get; set; } = string.Empty;
  public string ContentHash { get; set; } = string.Empty;
}
