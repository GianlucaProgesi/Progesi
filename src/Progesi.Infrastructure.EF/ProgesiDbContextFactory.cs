using Microsoft.EntityFrameworkCore;
using Progesi.Infrastructure.EF.Internal;

namespace Progesi.Infrastructure.EF;

public static class ProgesiDbContextFactory
{
  /// <summary>SQLite busy timeout (seconds) appended to connection strings when not already set.</summary>
  public const int DefaultBusyTimeoutSeconds = 5;

  public static ProgesiDbContext Create(string connectionString, bool resetSchema = false)
  {
    var options = BuildOptions(NormalizeConnectionString(connectionString));
    var context = new ProgesiDbContext(options);
    EnsureSchema(context, resetSchema);
    return context;
  }

  public static DbContextOptions<ProgesiDbContext> BuildOptions(string connectionString)
  {
    return new DbContextOptionsBuilder<ProgesiDbContext>()
        .UseSqlite(
            NormalizeConnectionString(connectionString),
            sqlite => sqlite.ExecutionStrategy(deps => new SqliteBusyRetryExecutionStrategy(deps)))
        .Options;
  }

  public static string NormalizeConnectionString(string connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      throw new ArgumentException("Connection string is required.", nameof(connectionString));

    if (connectionString.Contains("Default Timeout=", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("Busy Timeout=", StringComparison.OrdinalIgnoreCase))
      return connectionString;

    var separator = connectionString.TrimEnd().EndsWith(';') ? string.Empty : ";";
    return $"{connectionString}{separator}Default Timeout={DefaultBusyTimeoutSeconds}";
  }

  public static void EnsureSchema(ProgesiDbContext context, bool resetSchema = false)
  {
    if (resetSchema)
    {
      context.Database.EnsureDeleted();
    }

    context.Database.Migrate();
  }
}
