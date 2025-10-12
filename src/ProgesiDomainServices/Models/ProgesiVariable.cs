using System;

namespace Progesi.DomainServices.Models
{
  public class ProgesiVariable
  {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // <- init
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty; // <- init
    public string Type { get; set; } = string.Empty; // <- init
  }
}
