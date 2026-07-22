using System.Data.SQLite;

namespace Progesi.Data.EF
{
  public static class ProgesiEf
  {
    public static ProgesiDbContext Open(string dbPath)
    {
      var cn = new SQLiteConnection($"Data Source={dbPath};Foreign Keys=True;");
      cn.Open();
      return new ProgesiDbContext(cn, true);
    }
  }
}
