using System;
using System.Collections.Generic;
using System.Globalization;

namespace Progesi.DataExchange
{
  public sealed class ProgesiVariableDto
  {
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Hash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
    public string By { get; set; } = "";
    public string Ref { get; set; } = "";
    public string LastModifiedUtc { get; set; } = "";
  }

  public sealed class ProgesiMetadataDto
  {
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Hash { get; set; } = "";
    public string Info { get; set; } = "";
    public string By { get; set; } = "";
    public string Ref { get; set; } = "";
    public string LastModifiedUtc { get; set; } = "";
  }

  public sealed class ProgesiAxisVariableDto
  {
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string Hash { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public string AxisRef { get; set; } = "";
    public string Stations { get; set; } = ""; // "0;0.5;1"
    public string Values { get; set; } = "";   // "10;12;14"
    public string By { get; set; } = "";
    public string Ref { get; set; } = "";
    public string LastModifiedUtc { get; set; } = "";

    public static bool TryParseSeries(string s, out List<double> values)
    {
      values = new List<double>();
      if (string.IsNullOrWhiteSpace(s)) return true;
      var parts = s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var p in parts)
      {
        if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) values.Add(d);
        else return false;
      }
      return true;
    }
  }
}
