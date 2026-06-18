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
    public string ValueTypeKey { get; set; } = "System.Double"; // e.g. "System.Double"
    public string Unit { get; set; } = "";
    public string AxisRef { get; set; } = "";
    public string Stations { get; set; } = "";       // "0;0.5;1"  (sempre normalizzate)
    public string VariableHashes { get; set; } = ""; // "h1;h2;h3" (1:1 con Stations)
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

    public static bool TryParseTokens(string s, out List<string> tokens)
    {
      tokens = new List<string>();
      if (string.IsNullOrWhiteSpace(s)) return true;
      var parts = s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var p in parts)
      {
        var t = p.Trim();
        if (t.Length == 0) continue;
        tokens.Add(t);
      }
      return true;
    }

    public bool HasPairedStationsAndHashes()
    {
      if (!TryParseSeries(Stations, out var ss)) return false;
      if (!TryParseTokens(VariableHashes, out var hs)) return false;
      return ss.Count == hs.Count;
    }
  }
}
