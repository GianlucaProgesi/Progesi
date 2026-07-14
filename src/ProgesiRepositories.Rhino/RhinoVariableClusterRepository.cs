#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ProgesiCore;
using Rhino;
using Rhino.DocObjects.Tables;

namespace ProgesiRepositories.Rhino
{
  /// <summary>
  /// Repository Rhino per ProgesiVariableCluster.
  /// Salva i cluster nel RhinoDoc.Strings (StringTable) come JSON,
  /// sezione dedicata "Progesi.Cluster".
  /// </summary>
  public sealed class RhinoVariableClusterRepository : IProgesiVariableClusterRepository
  {
    private const string Section = "Progesi.Cluster";

    private readonly StringTable _table;

    public RhinoVariableClusterRepository(RhinoDoc doc)
    {
      if (doc is null) throw new ArgumentNullException(nameof(doc));
      _table = doc.Strings ?? throw new InvalidOperationException("RhinoDoc.Strings is null.");
    }

    // ======================= CRUD =======================

    public Task<ProgesiVariableCluster> SaveAsync(
      ProgesiVariableCluster cluster,
      CancellationToken ct = default)
    {
      if (cluster is null) throw new ArgumentNullException(nameof(cluster));

      var dto = new Dto
      {
        Id = cluster.Id,
        Name = cluster.Name,
        Description = cluster.Description,
        VariableIds = cluster.ProgesiVariableIds?.ToArray() ?? Array.Empty<int>(),
        Hashtag = cluster.Hashtag
      };

      string json = JsonConvert.SerializeObject(dto);
      _table.SetString(Section, KeyOf(cluster.Id), json);

      return Task.FromResult(cluster);
    }

    public Task<ProgesiVariableCluster?> GetByIdAsync(
      int id,
      CancellationToken ct = default)
    {
      if (id <= 0)
        return Task.FromResult<ProgesiVariableCluster?>(null);

      string key = KeyOf(id);
      string? json = _table.GetValue(Section, key);

      if (string.IsNullOrWhiteSpace(json))
        return Task.FromResult<ProgesiVariableCluster?>(null);

      var dto = JsonConvert.DeserializeObject<Dto>(json);
      if (dto == null || dto.Id <= 0)
        return Task.FromResult<ProgesiVariableCluster?>(null);

      int[] ids = dto.VariableIds ?? Array.Empty<int>();

      var cluster = ProgesiVariableCluster.Rehydrate(
        dto.Id,
        dto.Name ?? string.Empty,
        ids,
        dto.Description,
        dto.Hashtag);

      return Task.FromResult<ProgesiVariableCluster?>(cluster);
    }

    public async Task<ProgesiVariableCluster?> GetByHashtagAsync(
      string hashtag,
      CancellationToken ct = default)
    {
      if (string.IsNullOrWhiteSpace(hashtag))
        return null;

      var all = await GetAllAsync(ct).ConfigureAwait(false);
      var match = all.FirstOrDefault(c =>
        string.Equals(c.Hashtag, hashtag, StringComparison.Ordinal));

      return match;
    }

    public Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(
      CancellationToken ct = default)
    {
      var result = new List<ProgesiVariableCluster>();

      // Otteniamo tutti gli "entry names" della sezione dedicata ai cluster.
      string[] names = _table.GetEntryNames(Section) ?? Array.Empty<string>();

      foreach (var entry in names)
      {
        string? json = _table.GetValue(Section, entry);
        if (string.IsNullOrWhiteSpace(json))
          continue;

        Dto? dto;
        try
        {
          dto = JsonConvert.DeserializeObject<Dto>(json);
        }
        catch
        {
          // JSON corrotto o non riconosciuto: ignora questa entry.
          continue;
        }

        if (dto == null || dto.Id <= 0)
          continue;

        int[] ids = dto.VariableIds ?? Array.Empty<int>();

        var cluster = ProgesiVariableCluster.Rehydrate(
          dto.Id,
          dto.Name ?? string.Empty,
          ids,
          dto.Description,
          dto.Hashtag);

        result.Add(cluster);
      }

      // Ordiniamo per Id giusto per avere un output stabile.
      result.Sort((a, b) => a.Id.CompareTo(b.Id));

      return Task.FromResult<IReadOnlyList<ProgesiVariableCluster>>(result);
    }

    public Task<bool> DeleteAsync(
      int id,
      CancellationToken ct = default)
    {
      if (id <= 0)
        return Task.FromResult(false);

      string key = KeyOf(id);
      _table.Delete(Section, key);

      // Non abbiamo un ritorno booleano da Rhino, quindi assumiamo true.
      return Task.FromResult(true);
    }

    public Task<int> DeleteManyAsync(
      IEnumerable<int> ids,
      CancellationToken ct = default)
    {
      if (ids is null) throw new ArgumentNullException(nameof(ids));

      int count = 0;
      foreach (var id in ids)
      {
        if (id <= 0) continue;
        string key = KeyOf(id);
        _table.Delete(Section, key);
        count++;
      }

      return Task.FromResult(count);
    }

    private static string KeyOf(int id) => $"cluster:{id}";

    // DTO per la serializzazione su StringTable (JSON)
    private sealed class Dto
    {
      public int Id { get; set; }
      public string? Name { get; set; }
      public string? Description { get; set; }
      public int[]? VariableIds { get; set; }
      public string? Hashtag { get; set; }
    }
  }
}
