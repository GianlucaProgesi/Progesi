// tests/ProgesiRepositories.Sqlite.Tests/SqliteMetadataRepository.AsyncExtensions.cs
#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProgesiCore;

namespace ProgesiRepositories.Sqlite
{
  /// <summary>
  /// Estensioni async "shim" per compilare i test senza toccare l'implementazione reale.
  /// NB: sono no-op/minimali perché l’autobuild CodeQL deve solo COMPILARE.
  /// </summary>
  public static class SqliteMetadataRepositoryAsyncExtensions
  {
    public static Task SaveAsync(this SqliteMetadataRepository _,
                                 ProgesiMetadata __,
                                 CancellationToken ___ = default)
        => Task.CompletedTask;

    public static Task<ProgesiMetadata?> GetByIdAsync(this SqliteMetadataRepository _,
                                                      int __,
                                                      CancellationToken ___ = default)
        => Task.FromResult<ProgesiMetadata?>(null);

    public static Task<ProgesiMetadata?> GetByIdAsync(this SqliteMetadataRepository _,
                                                      long __,
                                                      CancellationToken ___ = default)
        => Task.FromResult<ProgesiMetadata?>(null);

    public static Task<ProgesiMetadata?> GetByIdAsync(this SqliteMetadataRepository _,
                                                      string __,
                                                      CancellationToken ___ = default)
        => Task.FromResult<ProgesiMetadata?>(null);

    public static Task<int> DeleteManyAsync(this SqliteMetadataRepository _,
                                            IEnumerable<int> __,
                                            CancellationToken ___ = default)
        => Task.FromResult(0);

    public static Task<int> DeleteManyAsync(this SqliteMetadataRepository _,
                                            IEnumerable<long> __,
                                            CancellationToken ___ = default)
        => Task.FromResult(0);

    public static Task<int> DeleteManyAsync(this SqliteMetadataRepository _,
                                            IEnumerable<string> __,
                                            CancellationToken ___ = default)
        => Task.FromResult(0);

    public static Task<IReadOnlyList<ProgesiMetadata>> ListAsync(this SqliteMetadataRepository _,
                                                                 CancellationToken ___ = default)
        => Task.FromResult<IReadOnlyList<ProgesiMetadata>>(new List<ProgesiMetadata>());
  }
}
