using System;
using ProgesiCore;

namespace ProgesiRepositories.Sqlite
{
    public static class SqliteVariableRepositoryLegacyExtensions
    {
        public static void Save(this SqliteVariableRepository repo, ProgesiVariable v)
            => repo.SaveAsync(v).GetAwaiter().GetResult();

        public static ProgesiVariable Load(this SqliteVariableRepository repo, int id)
            => repo.GetByIdAsync(id).GetAwaiter().GetResult();
    }
}
