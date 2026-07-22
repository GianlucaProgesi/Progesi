using FluentAssertions;
using Progesi.Infrastructure.EF;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class ProgesiDbContextTests : IDisposable
{
  private readonly ProgesiDbContext _context;
  private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

  public ProgesiDbContextTests()
  {
    (_context, _connection) = EfTestBootstrap.CreateIsolatedContext();
  }

  [Fact]
  public void EnsureCreated_Creates_Variable_And_Metadata_Tables()
  {
    _context.Variables.Should().NotBeNull();
    _context.Metadata.Should().NotBeNull();

    _context.Database.CanConnect().Should().BeTrue();
    _context.Variables.Count().Should().Be(0);
    _context.Metadata.Count().Should().Be(0);
  }

  public void Dispose()
  {
    _context.Dispose();
    _connection.Dispose();
  }
}
