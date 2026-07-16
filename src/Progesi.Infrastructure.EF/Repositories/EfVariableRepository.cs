using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using ProgesiCore;
using Progesi.Infrastructure.EF.Entities;
using Progesi.Infrastructure.EF.Internal;

namespace Progesi.Infrastructure.EF.Repositories;

public sealed class EfVariableRepository : IVariableRepository
{
  private readonly ProgesiDbContext _context;
  private readonly bool _ownsContext;

  public EfVariableRepository(ProgesiDbContext context, bool ownsContext = false)
  {
    _context = context ?? throw new ArgumentNullException(nameof(context));
    _ownsContext = ownsContext;
  }

  public EfVariableRepository(string connectionString, bool resetSchema = false)
      : this(ProgesiDbContextFactory.Create(connectionString, resetSchema), ownsContext: true)
  {
  }

  public async Task<ProgesiVariable> SaveAsync(ProgesiVariable variable, CancellationToken ct = default)
  {
    var hash = ProgesiHash.Compute(variable);

    var existing = await _context.Variables
        .AsNoTracking()
        .Where(v => v.ContentHash == hash)
        .Select(v => new { v.Id })
        .FirstOrDefaultAsync(ct);

    if (existing != null && existing.Id != variable.Id)
    {
      return await GetByIdAsync(existing.Id, ct) ?? variable;
    }

    var depends = (variable.DependsFrom ?? Array.Empty<int>()).ToArray();
    var entity = await _context.Variables.FindAsync(new object[] { variable.Id }, ct);

    if (entity == null)
    {
      entity = new VariableEntity { Id = variable.Id };
      _context.Variables.Add(entity);
    }

    entity.Name = variable.Name ?? string.Empty;
    entity.ValueType = ValueSerialization.TypeOf(variable.Value);
    entity.Value = ValueSerialization.Stringify(variable.Value);
    entity.MetadataId = variable.MetadataId;
    entity.DependsJson = JsonConvert.SerializeObject(depends);
    entity.ContentHash = hash;

    await _context.SaveChangesAsync(ct);

    return await GetByIdAsync(variable.Id, ct) ?? variable;
  }

  public async Task<ProgesiVariable> GetByIdAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Variables
        .AsNoTracking()
        .FirstOrDefaultAsync(v => v.Id == id, ct);

    if (entity == null) return null!;

    var depends = JsonConvert.DeserializeObject<int[]>(entity.DependsJson) ?? Array.Empty<int>();
    var value = ValueSerialization.ParseValue(entity.Value, entity.ValueType);
    return new ProgesiVariable(entity.Id, entity.Name, value, depends, entity.MetadataId);
  }

  public async Task<IReadOnlyList<ProgesiVariable>> GetAllAsync(CancellationToken ct = default)
  {
    var ids = await _context.Variables
        .AsNoTracking()
        .OrderBy(v => v.Id)
        .Select(v => v.Id)
        .ToListAsync(ct);

    var list = new List<ProgesiVariable>(ids.Count);
    foreach (var id in ids)
    {
      var variable = await GetByIdAsync(id, ct);
      if (variable != null) list.Add(variable);
    }

    return list;
  }

  public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
  {
    var entity = await _context.Variables.FindAsync(new object[] { id }, ct);
    if (entity == null) return false;

    _context.Variables.Remove(entity);
    await _context.SaveChangesAsync(ct);
    return true;
  }

  public async Task<int> DeleteManyAsync(IEnumerable<int> idsToDelete, CancellationToken ct = default)
  {
    if (idsToDelete == null) return 0;

    var ids = idsToDelete.ToArray();
    if (ids.Length == 0) return 0;

    var entities = await _context.Variables
        .Where(v => ids.Contains(v.Id))
        .ToListAsync(ct);

    if (entities.Count == 0) return 0;

    _context.Variables.RemoveRange(entities);
    await _context.SaveChangesAsync(ct);
    return entities.Count;
  }
}
