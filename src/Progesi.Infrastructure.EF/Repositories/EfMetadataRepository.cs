using Microsoft.EntityFrameworkCore;
using ProgesiCore;
using Progesi.Infrastructure.EF.Entities;
using Progesi.Infrastructure.EF.Internal;

namespace Progesi.Infrastructure.EF.Repositories;

public sealed class EfMetadataRepository : IMetadataRepository
{
  private readonly ProgesiDbContext _context;
  private readonly bool _ownsContext;

  public EfMetadataRepository(ProgesiDbContext context, bool ownsContext = false)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _ownsContext = ownsContext;
  }

  public EfMetadataRepository(string connectionString, bool resetSchema = false)
      : this(ProgesiDbContextFactory.Create(connectionString, resetSchema), ownsContext: true)
  {
  }

  public async Task<ProgesiMetadata?> GetAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Metadata
        .AsNoTracking()
        .FirstOrDefaultAsync(m => m.Id == id, ct);

    if (entity == null) return null;

    return MetadataSerialization.FromStoredJson(entity.Json, entity.LastModified, entity.Id);
  }

  public async Task UpsertAsync(ProgesiMetadata metadata, CancellationToken ct = default)
  {
    if (metadata is null) throw new ArgumentNullException(nameof(metadata));

    var contentHash = ProgesiHash.Compute(metadata);
    var json = MetadataSerialization.ToJson(metadata);
    var lastMod = metadata.LastModified.ToUniversalTime().ToString("o");

    var existingId = await GetIdByHashAsync(contentHash, ct);
    if (existingId > 0)
    {
      var existing = await _context.Metadata.FindAsync(new object[] { existingId }, ct);
      if (existing != null)
      {
        existing.LastModified = lastMod;
        await _context.SaveChangesAsync(ct);
      }

      return;
    }

    if (metadata.Id > 0)
    {
      var tracked = await _context.Metadata.FindAsync(new object[] { metadata.Id }, ct);
      if (tracked == null)
      {
        _context.Metadata.Add(new MetadataEntity
        {
          Id = metadata.Id,
          Json = json,
          LastModified = lastMod,
          ContentHash = contentHash
        });
        await _context.SaveChangesAsync(ct);
      }
    }
    else
    {
      _context.Metadata.Add(new MetadataEntity
      {
        Json = json,
        LastModified = lastMod,
        ContentHash = contentHash
      });
      await _context.SaveChangesAsync(ct);
    }
  }

  public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Metadata.FindAsync(new object[] { id }, ct);
    if (entity == null) return false;

    _context.Metadata.Remove(entity);
    await _context.SaveChangesAsync(ct);
    return true;
  }

  public async Task<IReadOnlyList<ProgesiMetadata>> ListAsync(int skip = 0, int take = 100, CancellationToken ct = default)
  {
    var ids = await _context.Metadata
        .AsNoTracking()
        .OrderBy(m => m.Id)
        .Skip(skip)
        .Take(take)
        .Select(m => m.Id)
        .ToListAsync(ct);

    var list = new List<ProgesiMetadata>(ids.Count);
    foreach (var id in ids)
    {
      var metadata = await GetAsync(id, ct);
      if (metadata != null) list.Add(metadata);
    }

    return list;
  }

  private async Task<int> GetIdByHashAsync(string contentHash, CancellationToken ct)
  {
    var id = await _context.Metadata
        .AsNoTracking()
        .Where(m => m.ContentHash == contentHash)
        .Select(m => (int?)m.Id)
        .FirstOrDefaultAsync(ct);

    return id ?? 0;
  }
}
