namespace Progesi.Infrastructure.EF.Entities;

public sealed class VariableEntity
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public string ValueType { get; set; } = string.Empty;
  public string Value { get; set; } = string.Empty;
  public int? MetadataId { get; set; }
  public string DependsJson { get; set; } = "[]";
  public string ContentHash { get; set; } = string.Empty;
}
