using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF;

namespace Progesi.Infrastructure.EF.Tests;

internal static class EfTestBootstrap
{
  public static (ProgesiDbContext Context, SqliteConnection Connection) CreateIsolatedContext()
  {
    var connection = new SqliteConnection($"Data Source=ef_test_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
    connection.Open();

    var options = new DbContextOptionsBuilder<ProgesiDbContext>()
        .UseSqlite(connection)
        .Options;

    var context = new ProgesiDbContext(options);
    ProgesiDbContextFactory.EnsureSchema(context, resetSchema: true);
    return (context, connection);
  }

  public static string CreateTempFileConnectionString()
  {
    var path = Path.Combine(Path.GetTempPath(), $"progesi_ef_{Guid.NewGuid():N}.sqlite");
    return $"Data Source={path}";
  }
}
