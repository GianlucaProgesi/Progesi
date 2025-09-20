using System;

namespace Progesi.DomainServices.Database
{
  // TODO: nella prossima micro-fase iniettiamo i repository reali (Rhino & SQLite)
  public class ProgesiDatabaseBridge : IProgesiDatabaseBridge
  {
    public void ExportRhinoToSqlite(string sqlitePath)
    {
      if (string.IsNullOrWhiteSpace(sqlitePath))
        throw new ArgumentException("sqlitePath is required", nameof(sqlitePath));

      // TODO: leggere da RhinoRepository e scrivere su SqliteRepository
      throw new NotImplementedException("Wiring con i repository (fase prossima).");
    }

    public void ImportSqliteToRhino(string sqlitePath)
    {
      if (string.IsNullOrWhiteSpace(sqlitePath))
        throw new ArgumentException("sqlitePath is required", nameof(sqlitePath));

      // TODO: leggere da SqliteRepository e scrivere su RhinoRepository
      throw new NotImplementedException("Wiring con i repository (fase prossima).");
    }
  }
}
