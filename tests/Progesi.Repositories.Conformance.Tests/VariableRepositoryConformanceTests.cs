using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using Xunit;

namespace Progesi.Repositories.Conformance.Tests
{
  public class VariableRepositoryConformanceTests
  {
    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task GetAllAsync_EmptyStore_Returns_Empty(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var all = await store.Repository.GetAllAsync();

      all.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task SaveAsync_Then_GetByIdAsync_RoundTrips_Explicit_Id(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      var original = new ProgesiVariable(5, "Span", 12.5, new[] { 1, 2 }, metadataId: 3);

      await repo.SaveAsync(original);
      var loaded = await repo.GetByIdAsync(5);

      loaded.Should().NotBeNull();
      loaded!.Id.Should().Be(5);
      loaded.Name.Should().Be("Span");
      loaded.Value.Should().Be(12.5);
      loaded.DependsFrom.Should().BeEquivalentTo(new[] { 1, 2 });
      loaded.MetadataId.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task GetByIdAsync_Missing_Id_Returns_Null(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var loaded = await store.Repository.GetByIdAsync(999);

      loaded.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task GetAllAsync_Returns_All_Saved_Variables(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      await repo.SaveAsync(new ProgesiVariable(1, "A", 1));
      await repo.SaveAsync(new ProgesiVariable(2, "B", 2));

      var all = await store.Repository.GetAllAsync();

      all.Should().HaveCount(2);
      all.Select(v => v.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task SaveAsync_Overwrite_Same_Id_Replaces_Stored_Variable(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      await repo.SaveAsync(new ProgesiVariable(7, "Before", 1));
      await repo.SaveAsync(new ProgesiVariable(7, "After", 9));

      var loaded = await repo.GetByIdAsync(7);

      loaded.Should().NotBeNull();
      loaded!.Name.Should().Be("After");
      loaded.Value.Should().Be(9);
      (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task SaveAsync_Preserves_Hash_Computable_Content(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      var original = new ProgesiVariable(11, "Load", 42, new[] { 3, 1, 2 }, metadataId: 4);

      await repo.SaveAsync(original);
      var loaded = await repo.GetByIdAsync(11);

      loaded.Should().NotBeNull();
      ProgesiHash.Compute(loaded!).Should().Be(ProgesiHash.Compute(original));
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task DeleteAsync_Existing_Id_Removes_Variable(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      await repo.SaveAsync(new ProgesiVariable(8, "Del", 0));

      (await repo.DeleteAsync(8)).Should().BeTrue();
      (await repo.GetByIdAsync(8)).Should().BeNull();
      (await repo.GetAllAsync()).Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task DeleteAsync_Missing_Id_Returns_False(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      (await store.Repository.DeleteAsync(404)).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task DeleteManyAsync_Removes_Existing_Ids(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var repo = store.Repository;
      await repo.SaveAsync(new ProgesiVariable(1, "A", 1));
      await repo.SaveAsync(new ProgesiVariable(2, "B", 2));
      await repo.SaveAsync(new ProgesiVariable(3, "C", 3));

      var removed = await repo.DeleteManyAsync(new[] { 1, 3, 99 });

      removed.Should().Be(2);
      (await repo.GetAllAsync()).Select(v => v.Id).Should().Equal(2);
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task DeleteManyAsync_Empty_Ids_Returns_Zero(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      var removed = await store.Repository.DeleteManyAsync(System.Array.Empty<int>());

      removed.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(VariableRepositoryConformanceStoreProvider.StoreFactories), MemberType = typeof(VariableRepositoryConformanceStoreProvider))]
    public async Task SaveAsync_With_CancellationToken_Completes(string storeName, System.Func<IVariableRepositoryConformanceStore> createStore)
    {
      using var store = createStore();
      using var cts = new CancellationTokenSource();
      var variable = new ProgesiVariable(20, "Tok", "x");

      var saved = await store.Repository.SaveAsync(variable, cts.Token);

      saved.Should().NotBeNull();
      saved.Id.Should().Be(20);
    }
  }
}
