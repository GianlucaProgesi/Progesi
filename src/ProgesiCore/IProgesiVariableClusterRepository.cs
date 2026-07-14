using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace ProgesiCore
{
  /// <summary>
  /// Repository astratto per la persistenza di ProgesiVariableCluster.
  /// Implementazioni: Rhino, SQLite, Excel, EF, ecc.
  /// </summary>
  public interface IProgesiVariableClusterRepository
  {
    /// <summary>
    /// Inserisce o aggiorna un cluster.
    /// Ritorna il cluster con Id assegnato/aggiornato e Hashtag coerente.
    /// </summary>
    Task<ProgesiVariableCluster> SaveAsync(
      ProgesiVariableCluster cluster,
      CancellationToken ct = default);

    /// <summary>
    /// Restituisce il cluster con Id specificato, oppure null se non esiste.
    /// </summary>
    Task<ProgesiVariableCluster?> GetByIdAsync(
      int id,
      CancellationToken ct = default);

    /// <summary>
    /// Restituisce il cluster con l'hashtag specificato, oppure null se non esiste.
    /// </summary>
    Task<ProgesiVariableCluster?> GetByHashtagAsync(
      string hashtag,
      CancellationToken ct = default);

    /// <summary>
    /// Restituisce tutti i cluster presenti nel repository.
    /// </summary>
    Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(
      CancellationToken ct = default);

    /// <summary>
    /// Elimina il cluster con Id specificato.
    /// </summary>
    Task<bool> DeleteAsync(
      int id,
      CancellationToken ct = default);

    /// <summary>
    /// Elimina in blocco i cluster con gli Id specificati.
    /// Ritorna il numero di record effettivamente eliminati.
    /// </summary>
    Task<int> DeleteManyAsync(
      IEnumerable<int> ids,
      CancellationToken ct = default);
  }
}
