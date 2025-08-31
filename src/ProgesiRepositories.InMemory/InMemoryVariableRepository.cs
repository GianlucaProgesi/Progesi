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

        public Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default)
        {
            _store[variable.Id] = variable;
            return Task.FromResult(variable);
        }

        public Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default)
        {
            _ = _store.TryGetValue(id, out ProgesiVariable v);
            return Task.FromResult(v);
        }

        public Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default)
        {
            IReadOnlyList<ProgesiVariable> list = _store.Values.ToList();
            return Task.FromResult(list);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            bool ok = _store.TryRemove(id, out _);
            return Task.FromResult(ok);
        }

        public Task<int> DeleteManyAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            int count = 0;
            foreach (int id in ids)
            {
                if (_store.TryRemove(id, out ProgesiVariable removed))
                {
                    count++;
                }
            }
            return Task.FromResult(count);
        }
    }
}
