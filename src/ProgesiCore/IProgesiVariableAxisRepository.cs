using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiCore
{
  /// <summary>
  /// Repository for persisting <see cref="ProgesiAxisVariable"/> (tiered SQLite / EF implementations).
  /// </summary>
  public interface IProgesiVariableAxisRepository
  {
    Task<ProgesiAxisVariable> SaveAsync(ProgesiAxisVariable axis, CancellationToken ct = default);

    Task<ProgesiAxisVariable?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<ProgesiAxisVariable?> GetByHashtagAsync(string hashtag, CancellationToken ct = default);

    Task<IReadOnlyList<ProgesiAxisVariable>> GetAllAsync(CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<int> DeleteManyAsync(IEnumerable<int> ids, CancellationToken ct = default);
  }
}
