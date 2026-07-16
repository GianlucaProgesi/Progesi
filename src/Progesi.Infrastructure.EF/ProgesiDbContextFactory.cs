using Microsoft.EntityFrameworkCore;

namespace Progesi.Infrastructure.EF;

public static class ProgesiDbContextFactory
{
  public static ProgesiDbContext Create(string connectionString, bool resetSchema = false)
  {
    var options = new DbContextOptionsBuilder<ProgesiDbContext>()
        .UseSqlite(connectionString)
        .Options;

    var context = new ProgesiDbContext(options);
    EnsureSchema(context, resetSchema);
    return context;
  }

  public static void EnsureSchema(ProgesiDbContext context, bool resetSchema = false)
  {
    if (resetSchema)
    {
      context.Database.EnsureDeleted();
    }

    context.Database.EnsureCreated();
  }
}
