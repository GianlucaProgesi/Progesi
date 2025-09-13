using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
  // Logger no-op per soddisfare il costruttore del repository
  public sealed class NoopLogger<T> : ILogger<T>
  {
    public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
    private sealed class NullDisposable : IDisposable { public static readonly NullDisposable Instance = new NullDisposable(); public void Dispose() { } }
  }

  public sealed class Coverage_SqliteRepos_ExtraTests : IDisposable
  {
    private readonly string _dbPath;
    private readonly SqliteVariableRepository _vars;
    private readonly SqliteMetadataRepository _meta;

    public Coverage_SqliteRepos_ExtraTests()
    {
      // Carica provider SQLite nativo dei test esistenti
      SqliteTestBootstrap.EnsureInitialized();

      _dbPath = Path.Combine(Path.GetTempPath(), $"progesi-test-{Guid.NewGuid():N}.db");

      // resetSchema:true ? copriamo i rami di creazione/cleanup schema nella base
      _vars = new SqliteVariableRepository(_dbPath, resetSchema: true, logger: new NoopLogger<SqliteVariableRepository>());
      _meta = new SqliteMetadataRepository(_dbPath, resetSchema: false);
    }

    public void Dispose()
    {
      try
      {
        if (File.Exists(_dbPath))
          File.Delete(_dbPath);
      }
      catch { /* ignora su CI */ }
    }

    private static ProgesiVariable MakeVar(int id, string name, string value, params int[] deps)
        => new ProgesiVariable(id, name, value, dependsFrom: deps);

    [Fact]
    public async Task Variable_Deduplicates_By_ContentHash()
    {
      // Due variabili con stesso contenuto ma ID diverso ? stesso ContentHash
      var v1 = MakeVar(1, "alpha", "A=1", 10, 11);
      var v2 = MakeVar(2, "alpha", "A=1", 10, 11); // dup

      var saved1 = await _vars.SaveAsync(v1);
      var saved2 = await _vars.SaveAsync(v2);

      saved1.Id.Should().Be(1);
      saved2.Id.Should().Be(1, "il secondo save con contenuto identico deve riusare la riga esistente");

      var all = await _vars.GetAllAsync();
      all.Should().HaveCount(1, "il deduplicate non deve creare una seconda riga");
      all[0].DependsFrom.Should().BeEquivalentTo(new[] { 10, 11 });
      all[0].Name.Should().Be("alpha");
      all[0].Value.Should().Be("A=1");
    }

    [Fact]
    public async Task Variable_GetAll_Sorted_And_DeleteMany_Works()
    {
      await _vars.SaveAsync(MakeVar(5, "v5", "x"));
      await _vars.SaveAsync(MakeVar(3, "v3", "x"));
      await _vars.SaveAsync(MakeVar(4, "v4", "x"));

      var list = await _vars.GetAllAsync();
      list.Select(v => v.Id).Should().ContainInOrder(3, 4, 5);

      await _vars.DeleteManyAsync(new[] { 3, 5 });
      var left = await _vars.GetAllAsync();
      left.Select(v => v.Id).Should().Equal(new[] { 4 });
    }

    [Fact]
    public async Task Metadata_Save_Update_List_DeleteMany()
    {
      var m1 = ProgesiMetadata.Create(id: 100, createdBy: "me", additionalInfo: "first");
      var m2 = ProgesiMetadata.Create(id: 200, createdBy: "you", additionalInfo: "second");

      await _meta.SaveAsync(m1);
      await _meta.SaveAsync(m2);

      var list1 = await _meta.ListAsync();
      list1.Select(m => m.Id).Should().Contain(new[] { 100, 200 });

      // update stesso ID
      var updated = ProgesiMetadata.Create(id: 100, createdBy: "me2", additionalInfo: "first-upd");
      await _meta.SaveAsync(updated);

      var fetched = await _meta.GetByIdAsync(100);
      fetched.Should().NotBeNull();
      fetched!.CreatedBy.Should().Be("me2");
      fetched!.AdditionalInfo.Should().Be("first-upd");

      await _meta.DeleteManyAsync(new[] { 100, 200 });
      var list2 = await _meta.ListAsync();
      list2.Should().BeEmpty();
    }
  }
}
