using System.Collections.Generic;

namespace Progesi.Repositories.Conformance.Tests
{
  public static class VariableRepositoryConformanceStoreProvider
  {
    public static IEnumerable<object[]> StoreFactories()
    {
      yield return new object[]
      {
        "InMemory",
        (System.Func<IVariableRepositoryConformanceStore>)(() => new InMemoryVariableRepositoryConformanceStore())
      };
      yield return new object[]
      {
        "Sqlite",
        (System.Func<IVariableRepositoryConformanceStore>)(() => new SqliteVariableRepositoryConformanceStore())
      };
    }
  }
}
