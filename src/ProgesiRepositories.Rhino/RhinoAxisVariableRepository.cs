#nullable enable
using Newtonsoft.Json;
using ProgesiCore;
using ProgesiCore.Serialization;
using Rhino;
using Rhino.DocObjects.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProgesiRepositories.Rhino
{
  /// <summary>
  /// StringTable-backed persistence for <see cref="ProgesiAxisVariable"/>.
  /// </summary>
  public sealed class RhinoAxisVariableRepository : IProgesiVariableAxisRepository
  {
    private const string AxisSection = "Progesi.Axis";
    private const string AxisHashSection = "Progesi.AxisHash";

    private readonly StringTable _table;

    public RhinoAxisVariableRepository(RhinoDoc doc)
    {
      if (doc is null) throw new ArgumentNullException(nameof(doc));
      _table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
    }

    public Task<ProgesiAxisVariable> SaveAsync(ProgesiAxisVariable axis, CancellationToken ct = default)
    {
      if (axis is null) throw new ArgumentNullException(nameof(axis));

      var hash = ProgesiHash.Compute(axis);
      var existingId = FindIdByContentHash(hash);
      if (existingId.HasValue && existingId.Value != axis.Id)
      {
        var existing = GetByIdAsync(existingId.Value, ct).GetAwaiter().GetResult();
        return Task.FromResult(existing!);
      }

      var current = axis.Id > 0 ? GetByIdAsync(axis.Id, ct).GetAwaiter().GetResult() : null;
      if (current != null)
        UnindexHashtag(current.Hashtag);

      var dto = ProgesiAxisVariableDto.FromDomain(axis);
      dto.ContentHash = hash;
      var json = JsonConvert.SerializeObject(dto) ?? string.Empty;
      _table.SetString(AxisSection, KeyOf(axis.Id), json);
      IndexHashtag(axis.Hashtag, axis.Id);
      return Task.FromResult(axis);
    }

    public Task<ProgesiAxisVariable?> GetByIdAsync(int id, CancellationToken ct = default)
    {
      if (id <= 0) return Task.FromResult<ProgesiAxisVariable?>(null);

      var json = _table.GetValue(AxisSection, KeyOf(id));
      if (string.IsNullOrWhiteSpace(json))
        return Task.FromResult<ProgesiAxisVariable?>(null);

      var dto = JsonConvert.DeserializeObject<ProgesiAxisVariableDto>(json);
      if (dto == null || dto.AxisId <= 0)
        return Task.FromResult<ProgesiAxisVariable?>(null);

      return Task.FromResult<ProgesiAxisVariable?>(ProgesiAxisVariableDto.ToDomain(dto));
    }

    public async Task<ProgesiAxisVariable?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      var idStr = _table.GetValue(AxisHashSection, hashtag);
      if (int.TryParse(idStr, out var id) && id > 0)
        return await GetByIdAsync(id, ct).ConfigureAwait(false);

      var all = await GetAllAsync(ct).ConfigureAwait(false);
      return all.FirstOrDefault(a =>
        string.Equals(a.Hashtag, hashtag, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(a.ContentHash, hashtag, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ProgesiAxisVariable>> GetAllAsync(CancellationToken ct = default)
    {
      var result = new List<ProgesiAxisVariable>();
      string[] names = _table.GetEntryNames(AxisSection) ?? Array.Empty<string>();

      foreach (var entry in names)
      {
        if (!TryParseAxisKey(entry, out var id))
          continue;

        var axis = await GetByIdAsync(id, ct).ConfigureAwait(false);
        if (axis != null)
          result.Add(axis);
      }

      result.Sort((a, b) => a.Id.CompareTo(b.Id));
      return result;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
      var key = KeyOf(id);
      var json = _table.GetValue(AxisSection, key);
      if (string.IsNullOrWhiteSpace(json))
        return Task.FromResult(false);

      var current = GetByIdAsync(id, ct).GetAwaiter().GetResult();
      if (current != null)
        UnindexHashtag(current.Hashtag);

      _table.Delete(AxisSection, key);
      return Task.FromResult(true);
    }

    public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
    {
      if (idsToDelete == null) return 0;
      int n = 0;
      foreach (var id in idsToDelete)
      {
        if (await DeleteAsync(id, ct).ConfigureAwait(false))
          n++;
      }
      return n;
    }

    private int? FindIdByContentHash(string hash)
    {
      if (string.IsNullOrWhiteSpace(hash)) return null;
      var idStr = _table.GetValue(AxisHashSection, hash);
      if (int.TryParse(idStr, out var id) && id > 0)
        return id;

      foreach (var entry in _table.GetEntryNames(AxisSection) ?? Array.Empty<string>())
      {
        if (!TryParseAxisKey(entry, out var axisId)) continue;
        var json = _table.GetValue(AxisSection, entry);
        if (string.IsNullOrWhiteSpace(json)) continue;
        var dto = JsonConvert.DeserializeObject<ProgesiAxisVariableDto>(json);
        if (dto != null && string.Equals(dto.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
          return axisId;
      }
      return null;
    }

    private static string KeyOf(int id) => $"axis:{id}";

    private static bool TryParseAxisKey(string key, out int id)
    {
      id = 0;
      const string prefix = "axis:";
      if (!key.StartsWith(prefix, StringComparison.Ordinal))
        return false;
      return int.TryParse(key.Substring(prefix.Length), out id) && id > 0;
    }

    private void IndexHashtag(string hashtag, int id)
    {
      if (string.IsNullOrWhiteSpace(hashtag) || id <= 0) return;
      _table.SetString(AxisHashSection, hashtag, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void UnindexHashtag(string hashtag)
    {
      if (string.IsNullOrWhiteSpace(hashtag)) return;
      _table.Delete(AxisHashSection, hashtag);
    }
  }
}
