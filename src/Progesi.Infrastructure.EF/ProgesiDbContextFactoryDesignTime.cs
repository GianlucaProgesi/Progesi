using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Progesi.Infrastructure.EF;

public sealed class ProgesiDbContextFactoryDesignTime : IDesignTimeDbContextFactory<ProgesiDbContext>
{
  public ProgesiDbContext CreateDbContext(string[] args)
  {
    var options = new DbContextOptionsBuilder<ProgesiDbContext>()
        .UseSqlite("Data Source=progesi_design_time.sqlite")
        .Options;

    return new ProgesiDbContext(options);
  }
}
