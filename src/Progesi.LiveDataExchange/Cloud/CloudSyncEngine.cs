using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Progesi.LiveDataExchange.Cloud
{
  public sealed class CloudSyncEngine
  {
    public async Task<CloudSyncResult> ExecuteAsync(
        CloudSyncDirection direction,
        CloudSnapshot local,
        CloudSnapshot cloud,
        ISyncStateStore syncState,
        IProgesiCloudClient cloudClient,
        ICloudSyncLocalApplier localApplier,
        bool propagateDeletions = false,
        CancellationToken ct = default)
    {
      if (local == null) throw new ArgumentNullException(nameof(local));
      if (cloud == null) throw new ArgumentNullException(nameof(cloud));
      if (syncState == null) throw new ArgumentNullException(nameof(syncState));
      if (cloudClient == null) throw new ArgumentNullException(nameof(cloudClient));
      if (localApplier == null) throw new ArgumentNullException(nameof(localApplier));

      var result = new CloudSyncResult();

      await ProcessTypeAsync(
          CloudSyncObjectType.Variable,
          local.Variables.ToDictionary(v => v.Id),
          cloud.Variables.ToDictionary(v => v.Id),
          syncState,
          direction,
          propagateDeletions,
          result,
          async record =>
          {
            await cloudClient.UpsertVariableAsync(record, ct).ConfigureAwait(false);
            result.VariablesApplied++;
          },
          async record =>
          {
            await localApplier.ApplyVariableAsync(record, ct).ConfigureAwait(false);
            result.VariablesApplied++;
          },
          async id =>
          {
            await cloudClient.DeleteVariableAsync(id, ct).ConfigureAwait(false);
            result.VariablesDeleted++;
          },
          async id =>
          {
            await localApplier.DeleteVariableAsync(id, ct).ConfigureAwait(false);
            result.VariablesDeleted++;
          }).ConfigureAwait(false);

      await ProcessTypeAsync(
          CloudSyncObjectType.Metadata,
          local.Metadata.ToDictionary(m => m.Id),
          cloud.Metadata.ToDictionary(m => m.Id),
          syncState,
          direction,
          propagateDeletions,
          result,
          async record =>
          {
            await cloudClient.UpsertMetadataAsync(record, ct).ConfigureAwait(false);
            result.MetadataApplied++;
          },
          async record =>
          {
            await localApplier.ApplyMetadataAsync(record, ct).ConfigureAwait(false);
            result.MetadataApplied++;
          },
          async id =>
          {
            await cloudClient.DeleteMetadataAsync(id, ct).ConfigureAwait(false);
            result.MetadataDeleted++;
          },
          async id =>
          {
            await localApplier.DeleteMetadataAsync(id, ct).ConfigureAwait(false);
            result.MetadataDeleted++;
          }).ConfigureAwait(false);

      await ProcessTypeAsync(
          CloudSyncObjectType.Cluster,
          local.Clusters.ToDictionary(c => c.Id),
          cloud.Clusters.ToDictionary(c => c.Id),
          syncState,
          direction,
          propagateDeletions,
          result,
          async record =>
          {
            await cloudClient.UpsertClusterAsync(record, ct).ConfigureAwait(false);
            result.ClustersApplied++;
          },
          async record =>
          {
            await localApplier.ApplyClusterAsync(record, ct).ConfigureAwait(false);
            result.ClustersApplied++;
          },
          async id =>
          {
            await cloudClient.DeleteClusterAsync(id, ct).ConfigureAwait(false);
            result.ClustersDeleted++;
          },
          async id =>
          {
            await localApplier.DeleteClusterAsync(id, ct).ConfigureAwait(false);
            result.ClustersDeleted++;
          }).ConfigureAwait(false);

      return result;
    }

    private static async Task ProcessTypeAsync<TRecord>(
        CloudSyncObjectType objectType,
        IReadOnlyDictionary<int, TRecord> localMap,
        IReadOnlyDictionary<int, TRecord> cloudMap,
        ISyncStateStore syncState,
        CloudSyncDirection direction,
        bool propagateDeletions,
        CloudSyncResult result,
        Func<TRecord, Task> applyToCloud,
        Func<TRecord, Task> applyToLocal,
        Func<int, Task> deleteFromCloud,
        Func<int, Task> deleteFromLocal)
        where TRecord : class
    {
      var ids = new HashSet<int>(localMap.Keys);
      ids.UnionWith(cloudMap.Keys);
      if (propagateDeletions)
      {
        foreach (var trackedId in syncState.GetTrackedIds(objectType))
          ids.Add(trackedId);
      }

      foreach (var id in ids.OrderBy(x => x))
      {
        localMap.TryGetValue(id, out var localRecord);
        cloudMap.TryGetValue(id, out var cloudRecord);

        var localHash = GetHash(localRecord);
        var cloudHash = GetHash(cloudRecord);
        var baseHash = syncState.GetLastSyncedHash(objectType, id) ?? string.Empty;

        var localExists = localRecord != null;
        var cloudExists = cloudRecord != null;
        var baseExisted = !string.IsNullOrEmpty(baseHash);

        if (!localExists && !cloudExists)
        {
          if (propagateDeletions && baseExisted)
          {
            syncState.RemoveLastSyncedHash(objectType, id);
            result.Skipped++;
            result.Log.Add(SkipMessage(objectType, id, "converged-deletion"));
          }

          continue;
        }

        if (propagateDeletions && baseExisted)
        {
          if (!localExists && cloudExists)
          {
            if (string.Equals(cloudHash, baseHash, StringComparison.Ordinal))
            {
              if (direction == CloudSyncDirection.Push)
              {
                await deleteFromCloud(id).ConfigureAwait(false);
                syncState.RemoveLastSyncedHash(objectType, id);
                result.Log.Add(DeletedMessage(objectType, id, "push"));
              }
              else
              {
                result.Skipped++;
                result.Log.Add(SkipMessage(objectType, id, "other-side-only-deletion"));
              }
            }
            else
            {
              result.Conflicts.Add(new CloudSyncConflict
              {
                ObjectType = objectType,
                Id = id,
                LocalHash = string.Empty,
                CloudHash = cloudHash ?? string.Empty,
                Kind = CloudSyncConflictKind.DeleteEdit
              });
              result.Log.Add(DeleteConflictMessage(objectType, id, localHash, cloudHash));
            }

            continue;
          }

          if (localExists && !cloudExists)
          {
            if (string.Equals(localHash, baseHash, StringComparison.Ordinal))
            {
              if (direction == CloudSyncDirection.Pull)
              {
                await deleteFromLocal(id).ConfigureAwait(false);
                syncState.RemoveLastSyncedHash(objectType, id);
                result.Log.Add(DeletedMessage(objectType, id, "pull"));
              }
              else
              {
                result.Skipped++;
                result.Log.Add(SkipMessage(objectType, id, "other-side-only-deletion"));
              }
            }
            else
            {
              result.Conflicts.Add(new CloudSyncConflict
              {
                ObjectType = objectType,
                Id = id,
                LocalHash = localHash ?? string.Empty,
                CloudHash = string.Empty,
                Kind = CloudSyncConflictKind.DeleteEdit
              });
              result.Log.Add(DeleteConflictMessage(objectType, id, localHash, cloudHash));
            }

            continue;
          }
        }

        var localChanged = localExists && !string.Equals(localHash, baseHash, StringComparison.Ordinal);
        var cloudChanged = cloudExists && !string.Equals(cloudHash, baseHash, StringComparison.Ordinal);

        if (!localChanged && !cloudChanged)
        {
          result.Skipped++;
          result.Log.Add(SkipMessage(objectType, id, "unchanged"));
          continue;
        }

        if (localChanged && cloudChanged)
        {
          if (string.Equals(localHash, cloudHash, StringComparison.Ordinal))
          {
            syncState.SetLastSyncedHash(objectType, id, localHash);
            result.Skipped++;
            result.Log.Add(SkipMessage(objectType, id, "converged"));
            continue;
          }

          result.Conflicts.Add(new CloudSyncConflict
          {
            ObjectType = objectType,
            Id = id,
            LocalHash = localHash ?? string.Empty,
            CloudHash = cloudHash ?? string.Empty,
            Kind = CloudSyncConflictKind.EditEdit
          });
          result.Log.Add(ConflictMessage(objectType, id, localHash, cloudHash));
          continue;
        }

        if (direction == CloudSyncDirection.Push && localChanged)
        {
          await applyToCloud(localRecord).ConfigureAwait(false);
          syncState.SetLastSyncedHash(objectType, id, localHash);
          result.Log.Add(AppliedMessage(objectType, id, "push"));
          continue;
        }

        if (direction == CloudSyncDirection.Pull && cloudChanged)
        {
          await applyToLocal(cloudRecord).ConfigureAwait(false);
          syncState.SetLastSyncedHash(objectType, id, cloudHash);
          result.Log.Add(AppliedMessage(objectType, id, "pull"));
          continue;
        }

        result.Skipped++;
        result.Log.Add(SkipMessage(objectType, id, "other-side-only-change"));
      }
    }

    private static string GetHash(object record)
    {
      if (record == null)
        return null;

      switch (record)
      {
        case CloudVariableRecord variable:
          return variable.ContentHash;
        case CloudMetadataRecord metadata:
          return metadata.ContentHash;
        case CloudClusterRecord cluster:
          return cluster.ContentHash;
        default:
          throw new ArgumentException("Unsupported record type.", nameof(record));
      }
    }

    private static string SkipMessage(CloudSyncObjectType type, int id, string reason)
        => type + " " + id + ": skipped (" + reason + ").";

    private static string AppliedMessage(CloudSyncObjectType type, int id, string direction)
        => type + " " + id + ": applied (" + direction + ").";

    private static string DeletedMessage(CloudSyncObjectType type, int id, string direction)
        => type + " " + id + ": deleted (" + direction + ").";

    private static string ConflictMessage(CloudSyncObjectType type, int id, string localHash, string cloudHash)
        => type + " " + id + ": CONFLICT local=" + localHash + " cloud=" + cloudHash + ".";

    private static string DeleteConflictMessage(CloudSyncObjectType type, int id, string localHash, string cloudHash)
        => type + " " + id + ": CONFLICT delete-vs-edit local=" + (localHash ?? string.Empty) + " cloud=" + (cloudHash ?? string.Empty) + ".";
  }
}
