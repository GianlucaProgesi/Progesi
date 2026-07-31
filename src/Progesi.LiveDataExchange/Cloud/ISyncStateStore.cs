namespace Progesi.LiveDataExchange.Cloud
{
  public interface ISyncStateStore
  {
    string GetLastSyncedHash(CloudSyncObjectType objectType, int id);
    void SetLastSyncedHash(CloudSyncObjectType objectType, int id, string contentHash);
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

    internal static string Key(CloudSyncObjectType objectType, int id)
        => objectType + ":" + id;
  }
}
