#nullable disable
namespace ProgesiRepositories.Sqlite
{
    public static class SqliteVariableRepositoryLegacyExtensions
    {
        public static ProgesiCore.ProgesiVariable GetById(this SqliteVariableRepository repo, int id)
            => repo.GetByIdAsync(id).GetAwaiter().GetResult();
    }
}
#nullable enable
