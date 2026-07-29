namespace Progesi.Infrastructure.EF.Entities;

public sealed class ClusterEntity
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string VariableIdsJson { get; set; } = "[]";
  public string ContentHash { get; set; } = string.Empty;
  public string Hashtag { get; set; } = string.Empty;
}
