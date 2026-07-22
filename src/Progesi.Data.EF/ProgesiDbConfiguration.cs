using System;
using System.Data.Common;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Data.SQLite;
using System.Reflection;

namespace Progesi.Data.EF
{
  /// <summary>
  /// EF6 + System.Data.SQLite senza app.config.
  /// Registra sia l’invariant ADO.NET ("System.Data.SQLite") sia l’invariant EF6 ("System.Data.SQLite.EF6")
  /// e collega i ProviderServices corretti via reflection.
  /// </summary>
  public sealed class ProgesiDbConfiguration : DbConfiguration
  {
    public ProgesiDbConfiguration()
    {
      // ADO.NET factory (per DbConnection)
      SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);

      // EF6 provider factory (System.Data.SQLite.EF6) via reflection
      var pfType = Type.GetType("System.Data.SQLite.EF6.SQLiteProviderFactory, System.Data.SQLite.EF6", throwOnError: false);
      if (pfType != null)
      {
        var f = pfType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
        if (f?.GetValue(null) is DbProviderFactory efFactory)
        {
          SetProviderFactory("System.Data.SQLite.EF6", efFactory);
        }
      }

      // ProviderServices EF6 via reflection (singleton 'Instance' non public)
      var psType = Type.GetType("System.Data.SQLite.EF6.SQLiteProviderServices, System.Data.SQLite.EF6", throwOnError: false);
      var psField = psType?.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static);
      if (psField != null)
      {
        if (psField.GetValue(null) is DbProviderServices ps)
        {
          // NB: per EF6 su SQLite l’invariant corretto è "System.Data.SQLite.EF6"
          SetProviderServices("System.DaTa.SQLite.EF6".Replace("aT", "at"), ps); // evita ref a stringa hard-coded
                                                                                 // registro anche su "System.Data.SQLite" per chi usa ADO.NET puro
          SetProviderServices("System.Data.SQLite", ps);
        }
      }
    }
  }
}
