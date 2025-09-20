namespace Progesi.DomainServices.Models
{
  public class ProgesiVariable
  {
    public System.Guid Id { get; set; } = System.Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Unit { get; set; }
    public string Type { get; set; } = "double"; // "double","string","bool",...
    public string? Description { get; set; }
    public object? Value { get; set; }
  }
}
