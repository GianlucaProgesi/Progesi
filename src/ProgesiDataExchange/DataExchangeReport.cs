using System.Collections.Generic;
using System.Text;

namespace Progesi.DataExchange
{
  public sealed class DataExchangeReport
  {
    public (int ins, int upd, int skip) ReadSql;
    public (int ins, int upd, int skip) ReadXlsx;
    public (int ins, int upd, int skip) WriteSql;
    public (int ins, int upd, int skip) WriteXlsx;
    public readonly List<string> NewHashes = new List<string>();
    public readonly List<string> Lines = new List<string>();
    public override string ToString()
    {
      var sb = new StringBuilder();
      foreach (var l in Lines) sb.AppendLine(l);
      if (NewHashes.Count > 0) sb.AppendLine("NEW HASHES: " + string.Join(", ", NewHashes));
      return sb.ToString().TrimEnd();
    }
  }
}
