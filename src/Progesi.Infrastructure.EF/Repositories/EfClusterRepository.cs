using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ProgesiCore;
using Progesi.Infrastructure.EF.Entities;

namespace Progesi.Infrastructure.EF.Repositories;

public sealed class EfClusterRepository : IProgesiVariableClusterRepository
{
  private readonly ProgesiDbContext _context;
  private readonly bool _ownsContext;

  public EfClusterRepository(ProgesiDbContext context, bool ownsContext = false)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _ownsContext = ownsContext;
  }

  public EfClusterRepository(string connectionString, bool resetSchema = false)
      : this(ProgesiDbContextFactory.Create(connectionString, resetSchema), ownsContext: true)
  {
  }

  public async Task<ProgesiVariableCluster> SaveAsync(ProgesiVariableCluster cluster, CancellationToken ct = default)
  {
    if (cluster is null) throw new ArgumentNullException(nameof(cluster));

    var hash = ProgesiHash.Compute(cluster);

    var existing = await _context.Clusters
        .AsNoTracking()
        .Where(c => c.ContentHash == hash)
        .Select(c => new { c.Id })
        .FirstOrDefaultAsync(ct);

    if (existing != null && existing.Id != cluster.Id)
    {
      return (await GetByIdAsync(existing.Id, ct))!;
    }

    var variableIdsJson = JsonConvert.SerializeObject(cluster.ProgesiVariableIds.ToArray());
    var hashtag = cluster.Hashtag ?? string.Empty;
    var entity = await _context.Clusters.FindAsync(new object[] { cluster.Id }, ct);

    if (entity == null)
    {
      entity = new ClusterEntity { Id = cluster.Id };
      _context.Clusters.Add(entity);
    }

    entity.Name = cluster.Name ?? string.Empty;
    entity.Description = cluster.Description ?? string.Empty;
    entity.VariableIdsJson = variableIdsJson;
    entity.ContentHash = hash;
    entity.Hashtag = hashtag;

    await _context.SaveChangesAsync(ct);

    return (await GetByIdAsync(cluster.Id, ct))!;
  }

  public async Task<ProgesiVariableCluster?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Clusters
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == id, ct);

    if (entity == null) return null;

    var ids = JsonConvert.DeserializeObject<int[]>(entity.VariableIdsJson) ?? Array.Empty<int>();
    return ProgesiVariableCluster.Rehydrate(
        entity.Id,
        entity.Name,
        ids,
        entity.Description,
        entity.Hashtag);
  }

  public async Task<ProgesiVariableCluster?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(hashtag))
      return null;

    var id = await _context.Clusters
        .AsNoTracking()
        .Where(c => c.Hashtag == hashtag)
        .Select(c => (int?)c.Id)
        .FirstOrDefaultAsync(ct);

    if (!id.HasValue)
    {
      var clusters = await _context.Clusters
          .AsNoTracking()
          .OrderBy(c => c.Id)
          .Select(c => new { c.Id, c.Name, c.VariableIdsJson })
          .ToListAsync(ct);

      foreach (var row in clusters)
      {
        var ids = JsonConvert.DeserializeObject<int[]>(row.VariableIdsJson) ?? Array.Empty<int>();
        var legacy = ProgesiVariableCluster.BuildLegacyHashtag(row.Id, row.Name, ids);
        if (string.Equals(legacy, hashtag, StringComparison.Ordinal))
        {
          id = row.Id;
          break;
        }
      }
    }

    if (!id.HasValue)
      return null;

    return await GetByIdAsync(id.Value, ct);
  }

  public async Task<IReadOnlyList<ProgesiVariableCluster>> GetAllAsync(CancellationToken ct = default)
  {
    var ids = await _context.Clusters
        .AsNoTracking()
        .OrderBy(c => c.Id)
        .Select(c => c.Id)
        .ToListAsync(ct);

    var list = new List<ProgesiVariableCluster>(ids.Count);
    foreach (var id in ids)
    {
      var cluster = await GetByIdAsync(id, ct);
      if (cluster != null) list.Add(cluster);
    }

    return list;
  }

  public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Clusters.FindAsync(new object[] { id }, ct);
    if (entity == null) return false;

    _context.Clusters.Remove(entity);
    await _context.SaveChangesAsync(ct);
    return true;
  }

  public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
  {
    if (idsToDelete == null) return 0;

    var ids = idsToDelete.ToArray();
    if (ids.Length == 0) return 0;

    var entities = await _context.Clusters
        .Where(c => ids.Contains(c.Id))
        .ToListAsync(ct);

    if (entities.Count == 0) return 0;

    _context.Clusters.RemoveRange(entities);
    await _context.SaveChangesAsync(ct);
    return entities.Count;
  }
}
