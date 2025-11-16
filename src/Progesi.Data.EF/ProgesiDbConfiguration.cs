using System;
using System.Data.Entity;
using System.Data.Entity.Core.Common;
using System.Reflection;
using System.Data.SQLite;

namespace Progesi.Data.EF
{
  /// <summary>
  /// EF6 DbConfiguration per System.Data.SQLite su .NET Framework 4.8
  /// - Registra la factory pubblica (SQLiteFactory.Instance)
  /// - Registra i provider services via reflection (evita CS0122 su SQLiteProviderServices)
  /// </summary>
  public sealed class ProgesiDbConfiguration : DbConfiguration
  {
    public ProgesiDbConfiguration()
    {
      // 1) Factory (questa è pubblica)
      SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);

      // 2) Provider services via reflection
      var t = Type.GetType("System.Data.SQLite.EF6.SQLiteProviderServices, System.Data.SQLite.EF6", throwOnError: false);
      if (t != null)
      {
        var instanceField = t.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static);
        if (instanceField != null)
        {
          var services = instanceField.GetValue(null) as DbProviderServices;
          if (services != null)
            SetProviderServices("System.Data.SQLite", services);
        }
      }
    }
  }
}
