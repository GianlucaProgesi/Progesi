using FluentAssertions;
using Progesi.Infrastructure.EF.Repositories;

namespace Progesi.Infrastructure.EF.Tests;

public sealed class EfRepositoryDisposalTests
{
  [Fact]
  public void ConnectionStringCtor_Disposes_Owned_DbContext()
  {
    var connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    var repo = new EfVariableRepository(connectionString, resetSchema: true);

    repo.Dispose();

    var act = () => repo.GetAllAsync().GetAwaiter().GetResult();
    act.Should().Throw<ObjectDisposedException>();
  }

  [Fact]
  public async Task ConnectionStringCtor_DisposeAsync_Disposes_Owned_DbContext()
  {
    var connectionString = EfTestBootstrap.CreateTempFileConnectionString();
    var repo = new EfMetadataRepository(connectionString, resetSchema: true);

    await repo.DisposeAsync();

    var act = async () => await repo.ListAsync();
    await act.Should().ThrowAsync<ObjectDisposedException>();
  }

  [Fact]
  public void InjectedContextCtor_Does_Not_Dispose_External_Context()
  {
    var (context, connection) = EfTestBootstrap.CreateIsolatedContext();
    try
    {
      var repo = new EfClusterRepository(context, ownsContext: false);
      repo.Dispose();

      context.Database.CanConnect().Should().BeTrue(
        because: "repos with ownsContext=false must not dispose an injected DbContext");
    }
    finally
    {
      context.Dispose();
      connection.Dispose();
    }
  }
}
