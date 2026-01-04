using FluentAssertions;
using Progesi.Data.EF;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Xunit;

namespace Progesi.Data.EF.Tests
{
  public class EfClusterJoinRoundTripTests
  {
    private static string NewTempDbPath()
    {
      var dir = Path.Combine(Path.GetTempPath(), "ProgesiEfTests");
      Directory.CreateDirectory(dir);
      return Path.Combine(dir, $"ef_{Guid.NewGuid():N}.db");
    }

    private static void CreateSchemaAndSeed(string dbPath)
    {
      using var cn = new SQLiteConnection($"Data Source={dbPath};Foreign Keys=True;");
      cn.Open();

      using var cmd = cn.CreateCommand();
      cmd.CommandText = @"
PRAGMA foreign_keys=ON;

CREATE TABLE IF NOT EXISTS Variables(
  Id INTEGER PRIMARY KEY,
  Hash TEXT NOT NULL,
  Name TEXT NOT NULL,
  Value TEXT,
  ValC TEXT,
  MetaId INTEGER,
  Assumption INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Clusters(
  Id INTEGER PRIMARY KEY,
  Hash TEXT NOT NULL,
  Name TEXT NOT NULL,
  Description TEXT
);

CREATE TABLE IF NOT EXISTS ClusterVariables(
  ClusterId INTEGER NOT NULL,
  VarId INTEGER NOT NULL,
  PRIMARY KEY (ClusterId, VarId),
  FOREIGN KEY (ClusterId) REFERENCES Clusters(Id) ON DELETE CASCADE,
  FOREIGN KEY (VarId)     REFERENCES Variables(Id) ON DELETE CASCADE
);

DELETE FROM ClusterVariables;
DELETE FROM Clusters;
DELETE FROM Variables;

INSERT INTO Variables(Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (1,'h1','Var-1','10','10',NULL,0);
INSERT INTO Variables(Id,Hash,Name,Value,ValC,MetaId,Assumption) VALUES (2,'h2','Var-2','20','20',NULL,0);

INSERT INTO Clusters(Id,Hash,Name,Description) VALUES (7,'hc','C7','desc');

INSERT INTO ClusterVariables(ClusterId,VarId) VALUES (7,1);
INSERT INTO ClusterVariables(ClusterId,VarId) VALUES (7,2);
";
      cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Cluster_join_roundtrip_works()
    {
      var dbPath = NewTempDbPath();
      try
      {
        CreateSchemaAndSeed(dbPath);

        using (var ctx = ProgesiEf.Open(dbPath))
        {
          ctx.Clusters.Count().Should().Be(1);
          ctx.ClusterVariables.Count().Should().Be(2);

          var joinIds = ctx.ClusterVariables
            .Where(x => x.ClusterId == 7)
            .Select(x => x.VarId)
            .OrderBy(x => x)
            .ToArray();

          joinIds.Should().Equal(new[] { 1, 2 });
        }
      }
      finally
      {
        try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { }
      }
    }
  }
}
