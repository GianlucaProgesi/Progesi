#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Progesi.DataExchange;
using ProgesiCore;
using ProgesiRepositories.Rhino;
using Rhino;

namespace Progesi.GrasshopperAssembly.Components
{
  internal static class RhinoBridgeBootstrap
  {
    private static bool _configured;
    public static void EnsureConfigured()
    {
      if (_configured) return;

      RhinoBridge.SetHandlers(
        getVars: GetAllVariables,
        getMets: GetAllMetadata,
        getAxis: GetAllAxisVariables,
        upsertVars: UpsertVariables,
        upsertMets: UpsertMetadata,
        upsertAxis: UpsertAxisVariables
      );
      _configured = true;
    }

    // ===== helpers =====
    private static int NextVariableId(RhinoVariableRepository repo)
    {
      IReadOnlyList<ProgesiVariable> all =
        repo.GetAllAsync().GetAwaiter().GetResult() ?? Array.Empty<ProgesiVariable>();

      return (all.Count == 0 ? 1 : all.Max(v => v.Id) + 1); // <-- Count, non Length
    }

    private static int NextMetadataId(RhinoMetadataRepository repo)
    {
      var all = new List<ProgesiMetadata>();
      int skip = 0; const int page = 500;
      while (true)
      {
        var chunk = repo.ListAsync(skip, page).GetAwaiter().GetResult();
        if (chunk == null || chunk.Count == 0) break;
        all.AddRange(chunk);
        if (chunk.Count < page) break;
        skip += page;
      }
      return (all.Count == 0 ? 1 : all.Max(m => m.Id) + 1);
    }

    // ========== GET ==========
    private static IReadOnlyList<ProgesiVariableDto> GetAllVariables()
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var repo = new RhinoVariableRepository(doc);
      var list = repo.GetAllAsync().GetAwaiter().GetResult() ?? Array.Empty<ProgesiVariable>();
      return list.Select(v => new ProgesiVariableDto
      {
        Id = v.Id.ToString(CultureInfo.InvariantCulture),
        Hash = ProgesiHash.Compute(v),
        Name = v.Name ?? string.Empty,
        Value = v.Value?.ToString() ?? string.Empty,
        Unit = "",
        By = "",
        Ref = "",
        LastModifiedUtc = ""
      }).ToList();
    }

    private static IReadOnlyList<ProgesiMetadataDto> GetAllMetadata()
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var repo = new RhinoMetadataRepository(doc);

      var all = new List<ProgesiMetadata>();
      int skip = 0; const int page = 500;
      while (true)
      {
        var chunk = repo.ListAsync(skip, page).GetAwaiter().GetResult();
        if (chunk == null || chunk.Count == 0) break;
        all.AddRange(chunk);
        if (chunk.Count < page) break;
        skip += page;
      }

      return all.Select(m => new ProgesiMetadataDto
      {
        Id = m.Id.ToString(CultureInfo.InvariantCulture),
        Hash = ProgesiHash.Compute(m),
        Info = m.AdditionalInfo ?? string.Empty,
        By = m.CreatedBy ?? string.Empty,
        Ref = string.Empty,
        LastModifiedUtc = m.LastModified.ToUniversalTime().ToString("s") + "Z"
      }).ToList();
    }

    private static IReadOnlyList<ProgesiAxisVariableDto> GetAllAxisVariables()
      => new List<ProgesiAxisVariableDto>(); // nessun repo Axis lato Rhino per ora

    // ========== UPSERT ==========
    private static (int ins, int upd, int skip) UpsertVariables(IEnumerable<ProgesiVariableDto> dtos)
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var repo = new RhinoVariableRepository(doc);

      int ins = 0, upd = 0, skip = 0;
      foreach (var d in dtos)
      {
        int id = 0; int.TryParse(d.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        ProgesiVariable? current = id > 0 ? repo.GetByIdAsync(id).GetAwaiter().GetResult() : null;

        if (current != null)
        {
          var curHash = ProgesiHash.Compute(current);
          if (string.Equals(curHash, d.Hash ?? "", StringComparison.OrdinalIgnoreCase))
          { skip++; continue; }
          id = 0;
        }
        if (id <= 0) id = NextVariableId(repo);

        var variable = new ProgesiVariable(id, d.Name ?? string.Empty, d.Value, Array.Empty<int>(), null);
        var _ = repo.SaveAsync(variable).GetAwaiter().GetResult();
        if (current == null) ins++; else upd++;
      }
      return (ins, upd, skip);
    }

    private static (int ins, int upd, int skip) UpsertMetadata(IEnumerable<ProgesiMetadataDto> dtos)
    {
      var doc = RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("RhinoDoc.ActiveDoc is null.");
      var repo = new RhinoMetadataRepository(doc);

      int ins = 0, upd = 0, skip = 0;
      foreach (var d in dtos)
      {
        int id = 0; int.TryParse(d.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        ProgesiMetadata? current = id > 0 ? repo.GetAsync(id).GetAwaiter().GetResult() : null;

        if (current != null)
        {
          var curHash = ProgesiHash.Compute(current);
          if (string.Equals(curHash, d.Hash ?? "", StringComparison.OrdinalIgnoreCase))
          { skip++; continue; }
          id = 0;
        }
        if (id <= 0) id = NextMetadataId(repo);

        var meta = ProgesiMetadata.Create(
          createdBy: d.By ?? string.Empty,
          additionalInfo: d.Info ?? string.Empty,
          references: null,
          snips: null,
          lastModifiedUtc: DateTime.UtcNow,
          id: id
        );
        repo.UpsertAsync(meta).GetAwaiter().GetResult();
        if (current == null) ins++; else upd++;
      }
      return (ins, upd, skip);
    }

    private static (int ins, int upd, int skip) UpsertAxisVariables(IEnumerable<ProgesiAxisVariableDto> dtos)
    {
      int count = dtos is ICollection<ProgesiAxisVariableDto> c ? c.Count : dtos.Count();
      return (0, 0, count); // niente repo Axis lato Rhino → tutto skip
    }
  }
}
