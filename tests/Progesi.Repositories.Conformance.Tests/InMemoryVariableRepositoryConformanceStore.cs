using ProgesiCore;
using ProgesiRepositories.InMemory;

namespace Progesi.Repositories.Conformance.Tests
{
  public sealed class InMemoryVariableRepositoryConformanceStore : IVariableRepositoryConformanceStore
  {
    public string StoreName => "InMemory";

    public IVariableRepository Repository { get; } = new InMemoryVariableRepository();

    public void Dispose()
    {
    }
  }
}
