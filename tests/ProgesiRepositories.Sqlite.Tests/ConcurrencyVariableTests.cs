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
    public class ConcurrencyVariableTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteVariableRepository _repo;

        public ConcurrencyVariableTests()
        {
            SqliteTestBootstrap.EnsureInitialized();
            _dbPath = Path.Combine(Path.GetTempPath(), $"progesi_var_conc_{Guid.NewGuid():N}.sqlite");
            _repo = new SqliteVariableRepository(_dbPath, resetSchema: true);
        }

        [Fact]
        public async Task Parallel_Saves_Do_Not_Create_Duplicates()
        {
            var tasks = Enumerable.Range(0, 8).Select(async _ =>
            {
                var v = new ProgesiVariable(1 + _, "A", 42, new[] { 3, 1, 2 }, metadataId: 7);
                await _repo.SaveAsync(v);
            });

            await Task.WhenAll(tasks);

            var all = await _repo.GetAllAsync();
            // tutti gli inserimenti hanno stesso contenuto -> 1 sola riga
            all.Should().HaveCount(1);
            all.Single().Name.Should().Be("A");
        }

        public void Dispose()
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }
    }
}
