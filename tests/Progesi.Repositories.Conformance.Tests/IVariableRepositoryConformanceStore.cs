using ProgesiCore;

namespace Progesi.Repositories.Conformance.Tests
{
  public interface IVariableRepositoryConformanceStore : System.IDisposable
  {
    string StoreName { get; }
    IVariableRepository Repository { get; }
  }
}
