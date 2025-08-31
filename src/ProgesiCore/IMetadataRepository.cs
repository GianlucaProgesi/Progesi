using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiCore
{
    /// <summary>
    /// Repository astratto per la persistenza di ProgesiMetadata.
    /// Implementazioni: Sqlite, Rhino, ecc.
    /// </summary>
    public interface IMetadataRepository
    {
        /// <summary>
        /// Restituisce la metadata con Id specificato, oppure null se non esiste.
        /// </summary>
        Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Inserisce o aggiorna la metadata.
        /// </summary>
        Task UpsertAsync(ProgesiMetadata metadata, CancellationToken ct = default);

        /// <summary>
        /// Elimina la metadata con Id specificato.
        /// </summary>
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>
        /// Elenca le metadata con paginazione semplice.
        /// </summary>
        Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default);
    }
}
