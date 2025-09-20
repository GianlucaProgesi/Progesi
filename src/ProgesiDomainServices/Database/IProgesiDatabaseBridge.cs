namespace Progesi.DomainServices.Database
{
  public interface IProgesiDatabaseBridge
  {
    // Esporta lo stato corrente (da Rhino) in un file SQLite
    void ExportRhinoToSqlite(string sqlitePath);

    // Importa un dump SQLite in Rhino
    void ImportSqliteToRhino(string sqlitePath);
  }
}
