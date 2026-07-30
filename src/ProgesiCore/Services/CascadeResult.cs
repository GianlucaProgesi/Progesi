using System.Collections.Generic;

namespace ProgesiCore.Services
{
  /// <summary>
  /// Outcome of a cascade-remove operation across multiple clusters.
  /// Partial failures are reported per cluster; the operation is idempotent and safe to retry.
  /// </summary>
  public sealed class CascadeResult
  {
    public CascadeResult(int applied, IReadOnlyList<int> failedClusterIds)
    {
      Applied = applied;
      FailedClusterIds = failedClusterIds ?? System.Array.Empty<int>();
    }

    public int Applied { get; }

    public IReadOnlyList<int> FailedClusterIds { get; }

    public bool IsFullySuccessful => FailedClusterIds.Count == 0;
  }
}
