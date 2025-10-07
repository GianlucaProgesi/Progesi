using System;
using System.IO;
using Microsoft.Data.Sqlite;

internal static class Program
{
  public static int Main(string[] args)
  {
    try
    {
      var outDir = "out/smoke";
      var reportPath = args is { Length: > 0 } ? args[0] : Path.Combine(outDir, "smoke-report.txt");
      Directory.CreateDirectory(outDir);
      var dbPath = Path.Combine(outDir, "progesi_smoke.db");
      if (File.Exists(dbPath)) File.Delete(dbPath);

      using var cn = new SqliteConnection($"Data Source={dbPath}");
      cn.Open();

      Exec(cn, @"
CREATE TABLE IF NOT EXISTS variables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, Value TEXT, Unit TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS metadata (
  Id TEXT PRIMARY KEY, Hash TEXT, Info TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE TABLE IF NOT EXISTS axisvariables (
  Id TEXT PRIMARY KEY, Hash TEXT, Name TEXT, Unit TEXT, AxisRef TEXT, Stations TEXT, ""Values"" TEXT, By TEXT, Ref TEXT, LastModifiedUtc TEXT
);
CREATE INDEX IF NOT EXISTS idx_variables_hash ON variables(Hash);
CREATE INDEX IF NOT EXISTS idx_metadata_hash ON metadata(Hash);
CREATE INDEX IF NOT EXISTS idx_axis_hash     ON axisvariables(Hash);
");

      Exec(cn, @"
INSERT INTO variables (Id,Hash,Name,Value,Unit,By,Ref,LastModifiedUtc)
VALUES ('v-1','','E1_Load','42.5','kN','Smoke','','2020-01-01T00:00:00Z');
INSERT INTO metadata (Id,Hash,Info,By,Ref,LastModifiedUtc)
VALUES ('m-1','','Seed metadata: project alpha','Smoke','','2020-01-01T00:00:00Z');
INSERT INTO axisvariables (Id,Hash,Name,Unit,AxisRef,Stations,""Values"",By,Ref,LastModifiedUtc)
VALUES ('a-1','','GirderCamber','mm','Axis-1','0;0.5;1','0;15;0','Smoke','','2020-01-01T00:00:00Z');
");

      bool ok = true;
      using var cmd = cn.CreateCommand();

      cmd.CommandText = "SELECT COUNT(*) FROM variables WHERE Name='E1_Load' AND Unit='kN' AND Value='42.5'";
      var v = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

      cmd.CommandText = "SELECT COUNT(*) FROM metadata WHERE Info LIKE '%Seed metadata%'";
      var m = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

      cmd.CommandText = "SELECT COUNT(*) FROM axisvariables WHERE Name='GirderCamber' AND Stations='0;0.5;1' AND \"Values\"='0;15;0'";
      var a = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);

      using var sw = new StreamWriter(reportPath, false);
      if (v == 1) sw.WriteLine("OK variables: E1_Load=42.5 kN"); else { sw.WriteLine($"FAIL variables: atteso 1, trovato {v}"); ok = false; }
      if (m >= 1) sw.WriteLine($"OK metadata: seed presente ({m})"); else { sw.WriteLine("FAIL metadata: seed mancante"); ok = false; }
      if (a == 1) sw.WriteLine("OK axis: GirderCamber con stations/values corretti"); else { sw.WriteLine($"FAIL axis: atteso 1, trovato {a}"); ok = false; }
      sw.WriteLine($"Smoke P0 - {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}  Result: {(ok ? "PASS" : "FAIL")}");

      Console.WriteLine(ok ? "SMOKE PASS" : "SMOKE FAIL");
      Console.WriteLine($"Report: {reportPath}");
      return ok ? 0 : 1;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine(ex.ToString());
      return 1;
    }
  }

  private static void Exec(SqliteConnection cn, string sql)
  {
    using var c = cn.CreateCommand();
    c.CommandText = sql;
    c.ExecuteNonQuery();
  }
}
