using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using ProgesiCore;
using ProgesiRepositories.Sqlite;
using Xunit;

namespace ProgesiRepositories.Sqlite.Tests
{
    public class SqliteVariableRepositoryTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteVariableRepository _repo;

        public SqliteVariableRepositoryTests()
        {
            SqliteTestBootstrap.EnsureInitialized();

            _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_var_{Guid.NewGuid():N}.sqlite");
            _repo = new SqliteVariableRepository(_dbPath, resetSchema: true);

        }

        [Fact]
        public async Task Can_Create_And_Save_Variable()
        {
            var v = new ProgesiVariable(1, "A", 42, new[] { 1, 2, 3 }, metadataId: 7);

            var saved = await _repo.SaveAsync(v);
            saved.Id.Should().Be(1);
            saved.Name.Should().Be("A");

            var back = await _repo.GetByIdAsync(1);
            back.Should().NotBeNull();
            back!.Value.Should().Be(42);
            back.DependsFrom.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        }

        [Fact]
        public async Task Save_Deduplicates_By_ContentHash()
        {
            var v1 = new ProgesiVariable(1, "A", 42, new[] { 3, 1, 2 }, metadataId: 7);
            await _repo.SaveAsync(v1);

            var v2 = new ProgesiVariable(2, "A", 42, new[] { 2, 3, 1 }, metadataId: 7);
            var ret = await _repo.SaveAsync(v2);

            ret.Id.Should().Be(1);

            var all = await _repo.GetAllAsync();
            all.Select(x => x.Id).Should().Contain(1).And.NotContain(2);
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
