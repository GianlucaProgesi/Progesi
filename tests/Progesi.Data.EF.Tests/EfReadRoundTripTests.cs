using FluentAssertions;
using Progesi.Data.EF;
using System.IO;
using System.Linq;
using Xunit;

namespace Progesi.Data.EF.Tests
{
  public class EfReadRoundTripTests
  {
    [Fact]
    public void EF_can_read_clusters_and_join_rows()
    {
      // Punto al DB che hai già creato con DataEx ExportSqlite
      // Se il tuo DataEx crea il file senza estensione, metti il path esatto.
      var dbPath = @"C:\GH_tool\EfRoundTrip-1";

      File.Exists(dbPath).Should().BeTrue("DB file must exist. If DataEx created EfRoundTrip-1.db, update the path.");

      using (var ctx = ProgesiEf.Open(dbPath))
      {
        // deve leggere i cluster
        ctx.Clusters.Count().Should().BeGreaterThan(0);

        // join rows devono esserci
        ctx.ClusterVariables.Count().Should().BeGreaterThan(0);

        // sanity: ogni join row deve puntare a un cluster esistente
        var clusterIds = ctx.Clusters.Select(c => c.Id).ToList();
        ctx.ClusterVariables.All(j => clusterIds.Contains(j.ClusterId)).Should().BeTrue();
      }
    }
  }
}
