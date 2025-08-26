using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiCore
{
    public interface IVariableRepository
    {
        Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default);  // upsert
        Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
        Task<int> DeleteManyAsync(IEnumerable<int> ids, CancellationToken ct = default);
    }  
}
