namespace Progesi.LiveDataExchange.Cloud
{
  public interface ISyncStateStore
  {
    string GetLastSyncedHash(CloudSyncObjectType objectType, int id);
    void SetLastSyncedHash(CloudSyncObjectType objectType, int id, string contentHash);
    void RemoveLastSyncedHash(CloudSyncObjectType objectType, int id);
    System.Collections.Generic.IEnumerable<int> GetTrackedIds(CloudSyncObjectType objectType);
  }

  public sealed class InMemorySyncStateStore : ISyncStateStore
  {
    private readonly System.Collections.Generic.Dictionary<string, string> _hashes
        = new System.Collections.Generic.Dictionary<string, string>();

    public string GetLastSyncedHash(CloudSyncObjectType objectType, int id)
    {
      return _hashes.TryGetValue(Key(objectType, id), out var hash) ? hash : string.Empty;
    }

    public void SetLastSyncedHash(CloudSyncObjectType objectType, int id, string contentHash)
    {
      _hashes[Key(objectType, id)] = contentHash ?? string.Empty;
    }

    public void RemoveLastSyncedHash(CloudSyncObjectType objectType, int id)
    {
      _hashes.Remove(Key(objectType, id));
    }

    public System.Collections.Generic.IEnumerable<int> GetTrackedIds(CloudSyncObjectType objectType)
    {
      var prefix = objectType + ":";
      foreach (var entry in _hashes.Keys)
      {
        if (!entry.StartsWith(prefix, System.StringComparison.Ordinal))
          continue;

        if (int.TryParse(entry.Substring(prefix.Length), out var id))
          yield return id;
      }
    }

    internal static string Key(CloudSyncObjectType objectType, int id)
        => objectType + ":" + id;
  }
}
