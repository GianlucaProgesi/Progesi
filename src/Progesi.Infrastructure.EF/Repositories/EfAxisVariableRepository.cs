using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ProgesiCore;
using ProgesiCore.Serialization;
using Progesi.Infrastructure.EF.Entities;

namespace Progesi.Infrastructure.EF.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProgesiVariableAxisRepository"/> for the web tier.
/// Not thread-safe — scope one instance (and its DbContext) per request/unit-of-work.
/// </summary>
public sealed class EfAxisVariableRepository : IProgesiVariableAxisRepository, IDisposable, IAsyncDisposable
{
  private readonly ProgesiDbContext _context;
  private readonly bool _ownsContext;

  public EfAxisVariableRepository(ProgesiDbContext context, bool ownsContext = false)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _ownsContext = ownsContext;
  }

  public EfAxisVariableRepository(string connectionString, bool resetSchema = false)
      : this(ProgesiDbContextFactory.Create(connectionString, resetSchema), ownsContext: true)
  {
  }

  public async Task<ProgesiAxisVariable> SaveAsync(ProgesiAxisVariable axis, CancellationToken ct = default)
  {
    if (axis is null) throw new ArgumentNullException(nameof(axis));

    var hash = ProgesiHash.Compute(axis);
    var dto = ProgesiAxisVariableDto.FromDomain(axis);

    var existing = await _context.Axis
        .AsNoTracking()
        .Where(a => a.ContentHash == hash)
        .Select(a => new { a.Id })
        .FirstOrDefaultAsync(ct);

    if (existing != null && existing.Id != axis.Id)
      return (await GetByIdAsync(existing.Id, ct))!;

    var entity = await _context.Axis.FindAsync(new object[] { axis.Id }, ct);
    if (entity == null)
    {
      entity = new AxisEntity { Id = axis.Id };
      _context.Axis.Add(entity);
    }

    entity.AxisName = dto.AxisName ?? string.Empty;
    entity.Name = dto.Name ?? string.Empty;
    entity.ValueTypeKey = dto.ValueTypeKey ?? string.Empty;
    entity.AxisLength = dto.AxisLength;
    entity.CurvePayload = dto.CurvePayload ?? string.Empty;
    entity.Mode = (int)dto.Mode;
    entity.KeyPointsJson = JsonConvert.SerializeObject(dto.KeyPoints ?? new List<double>());
    entity.RuleId = dto.RuleId;
    entity.FunctionId = dto.FunctionId;
    entity.FunctionHashtag = dto.FunctionHashtag;
    entity.FunctionPayload = dto.FunctionPayload ?? string.Empty;
    entity.StationsJson = SerializeStations(dto.Entries);
    entity.LabelsJson = SerializeLabels(dto.Labels);
    entity.ContentHash = hash;
    entity.Hashtag = axis.Hashtag ?? string.Empty;

    await _context.SaveChangesAsync(ct);
    return (await GetByIdAsync(axis.Id, ct))!;
  }

  public async Task<ProgesiAxisVariable?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Axis
        .AsNoTracking()
        .FirstOrDefaultAsync(a => a.Id == id, ct);

    if (entity == null) return null;
    return ToDomain(entity);
  }

  public async Task<ProgesiAxisVariable?> GetByHashtagAsync(string hashtag, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(hashtag))
      return null;

    var id = await _context.Axis
        .AsNoTracking()
        .Where(a => a.Hashtag == hashtag)
        .Select(a => (int?)a.Id)
        .FirstOrDefaultAsync(ct);

    if (!id.HasValue)
    {
      id = await _context.Axis
          .AsNoTracking()
          .Where(a => a.ContentHash == hashtag)
          .Select(a => (int?)a.Id)
          .FirstOrDefaultAsync(ct);
    }

    if (!id.HasValue)
      return null;

    return await GetByIdAsync(id.Value, ct);
  }

  public async Task<IReadOnlyList<ProgesiAxisVariable>> GetAllAsync(CancellationToken ct = default)
  {
    var ids = await _context.Axis
        .AsNoTracking()
        .OrderBy(a => a.Id)
        .Select(a => a.Id)
        .ToListAsync(ct);

    var list = new List<ProgesiAxisVariable>(ids.Count);
    foreach (var id in ids)
    {
      var axis = await GetByIdAsync(id, ct);
      if (axis != null) list.Add(axis);
    }

    return list;
  }

  public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Axis.FindAsync(new object[] { id }, ct);
    if (entity == null) return false;

    _context.Axis.Remove(entity);
    await _context.SaveChangesAsync(ct);
    return true;
  }

  public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
  {
    if (idsToDelete == null) return 0;

    var ids = idsToDelete.ToArray();
    if (ids.Length == 0) return 0;

    var entities = await _context.Axis
        .Where(a => ids.Contains(a.Id))
        .ToListAsync(ct);

    if (entities.Count == 0) return 0;

    _context.Axis.RemoveRange(entities);
    await _context.SaveChangesAsync(ct);
    return entities.Count;
  }

  public void Dispose()
  {
    if (_ownsContext)
      _context.Dispose();
  }

  public async ValueTask DisposeAsync()
  {
    if (_ownsContext)
      await _context.DisposeAsync().ConfigureAwait(false);
  }

  private static ProgesiAxisVariable ToDomain(AxisEntity entity)
  {
    var dto = new ProgesiAxisVariableDto
    {
      AxisId = entity.Id,
      AxisName = entity.AxisName,
      Name = entity.Name,
      ValueTypeKey = entity.ValueTypeKey,
      AxisLength = entity.AxisLength,
      CurvePayload = entity.CurvePayload,
      Mode = (AxisCurveMode)entity.Mode,
      KeyPoints = JsonConvert.DeserializeObject<List<double>>(entity.KeyPointsJson) ?? new List<double>(),
      RuleId = entity.RuleId,
      FunctionId = entity.FunctionId,
      FunctionHashtag = entity.FunctionHashtag,
      FunctionPayload = string.IsNullOrWhiteSpace(entity.FunctionPayload) ? null : entity.FunctionPayload,
      ContentHash = entity.ContentHash,
      Entries = DeserializeStations(entity.StationsJson),
      Labels = DeserializeLabels(entity.LabelsJson)
    };

    return ProgesiAxisVariableDto.ToDomain(dto);
  }

  private sealed class StationJsonEntry
  {
    public double Position { get; set; }
    public int VariableId { get; set; }
    public ProgesiAxisStationSide Side { get; set; } = ProgesiAxisStationSide.None;
  }

  private sealed class LabelJsonEntry
  {
    public double Position { get; set; }
    public string Label { get; set; } = string.Empty;
  }

  private static string SerializeStations(IEnumerable<ProgesiAxisVariableDto.Entry> entries)
  {
    var payload = entries
      .Select(e => new StationJsonEntry { Position = e.Position, VariableId = e.VariableId, Side = e.Side })
      .OrderBy(e => e.Position)
      .ThenBy(e => e.Side)
      .ThenBy(e => e.VariableId)
      .ToArray();
    return JsonConvert.SerializeObject(payload);
  }

  private static List<ProgesiAxisVariableDto.Entry> DeserializeStations(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return new List<ProgesiAxisVariableDto.Entry>();

    var rows = JsonConvert.DeserializeObject<StationJsonEntry[]>(json) ?? Array.Empty<StationJsonEntry>();
    return rows.Select(r => new ProgesiAxisVariableDto.Entry
    {
      Position = r.Position,
      VariableId = r.VariableId,
      Side = r.Side
    }).ToList();
  }

  private static string SerializeLabels(IEnumerable<ProgesiAxisVariableDto.LabelEntry> labels)
  {
    var payload = labels
      .Where(l => !string.IsNullOrWhiteSpace(l.Label))
      .Select(l => new LabelJsonEntry { Position = l.Position, Label = l.Label })
      .OrderBy(l => l.Position)
      .ToArray();
    return JsonConvert.SerializeObject(payload);
  }

  private static List<ProgesiAxisVariableDto.LabelEntry> DeserializeLabels(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return new List<ProgesiAxisVariableDto.LabelEntry>();

    var rows = JsonConvert.DeserializeObject<LabelJsonEntry[]>(json) ?? Array.Empty<LabelJsonEntry>();
    return rows.Select(r => new ProgesiAxisVariableDto.LabelEntry
    {
      Position = r.Position,
      Label = r.Label
    }).ToList();
  }
}
