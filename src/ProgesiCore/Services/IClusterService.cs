using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace ProgesiCore.Services
{
  /// <summary>
  /// Servizio applicativo per lavorare con i ProgesiVariableCluster
  /// sopra a qualunque implementazione di IProgesiVariableClusterRepository
  /// (InMemory, SQLite, Rhino, EF, ...).
  /// </summary>
  public interface IClusterService
  {
    /// <summary>
    /// Crea un nuovo cluster oppure restituisce quello esistente
    /// logicamente equivalente (stesso Name, Description e stesso set di Id).
    /// </summary>
    Task<ProgesiVariableCluster> CreateOrGetClusterAsync(
      string name,
      IEnumerable<int> progesiVariableIds,
      string? description = null,
      CancellationToken ct = default);

    Task<ProgesiVariableCluster?> GetByIdAsync(
      int id,
      CancellationToken ct = default);

    Task<ProgesiVariableCluster?> GetByHashtagAsync(
      string hashtag,
      CancellationToken ct = default);

    Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(
      CancellationToken ct = default);

    Task<bool> DeleteAsync(
      int id,
      CancellationToken ct = default);

    Task<int> DeleteManyAsync(
      IEnumerable<int> ids,
      CancellationToken ct = default);
  }
}
