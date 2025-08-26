using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProgesiCore;

namespace ProgesiRepositories.InMemory
{
    public class InMemoryVariableRepository : IVariableRepository
    {
        private readonly ConcurrentDictionary<int, ProgesiVariable> _store = new ConcurrentDictionary<int, ProgesiVariable>();

        public Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default(CancellationToken))
        {
            _store[variable.Id] = variable;
            return Task.FromResult(variable);
        }

        public Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default(CancellationToken))
        {
            ProgesiVariable v;
            _store.TryGetValue(id, out v);
            return Task.FromResult(v);
        }

        public Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default(CancellationToken))
        {
            IReadOnlyList<ProgesiVariable> list = _store.Values.ToList();
            return Task.FromResult(list);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default(CancellationToken))
        {
            ProgesiVariable removed;
            bool ok = _store.TryRemove(id, out removed);
            return Task.FromResult(ok);
        }

        public Task<int> DeleteManyAsync(IEnumerable<int> ids, CancellationToken ct = default(CancellationToken))
        {
            int count = 0;
            foreach (var id in ids)
            {
                ProgesiVariable removed;
                if (_store.TryRemove(id, out removed)) count++;
            }
            return Task.FromResult(count);
        }
    }
}
