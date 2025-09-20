using System;

namespace Progesi.DomainServices.Models
{
  public class ProgesiVariable
  {
    public Guid Id { get; set; }
    public string Name { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
    public string Type { get; set; }
  }
}
