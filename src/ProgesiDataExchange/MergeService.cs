using System;
using System.Collections.Generic;
using System.Linq;

namespace Progesi.DataExchange
{
  internal static class MergeService
  {
    public static (int ins, int upd, int skip, List<string> newHashes)
      MergeVariables(IEnumerable<ProgesiVariableDto> src, IProgesiStore target)
    {
      var newHashes = new List<string>();
      var existing = target.GetAllVariables();
      var byId = existing.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
      var byHash = existing.GroupBy(x => x.Hash ?? "")
                           .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

      var toUpsert = new List<ProgesiVariableDto>(); int ins = 0, skip = 0;
      foreach (var it in src)
      {
        if (!string.IsNullOrWhiteSpace(it.Id) && byId.TryGetValue(it.Id, out var cur))
        {
          if (string.Equals(cur.Hash ?? "", it.Hash ?? "", StringComparison.OrdinalIgnoreCase))
          { skip++; toUpsert.Add(it); continue; }
          it.Id = Guid.NewGuid().ToString("D"); ins++; toUpsert.Add(it);
          if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("v/" + it.Hash);
          continue;
        }
        if (!string.IsNullOrWhiteSpace(it.Hash) && byHash.ContainsKey(it.Hash)) { skip++; continue; }
        ins++; toUpsert.Add(it);
        if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("v/" + it.Hash);
      }

      var res = target.UpsertVariables(toUpsert);
      return (res.inserted, res.updated, res.skipped, newHashes);
    }

    public static (int ins, int upd, int skip, List<string> newHashes)
      MergeMetadata(IEnumerable<ProgesiMetadataDto> src, IProgesiStore target)
    {
      var newHashes = new List<string>();
      var existing = target.GetAllMetadata();
      var byId = existing.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
      var byHash = existing.GroupBy(x => x.Hash ?? "")
                           .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

      var toUpsert = new List<ProgesiMetadataDto>(); int ins = 0, skip = 0;
      foreach (var it in src)
      {
        if (!string.IsNullOrWhiteSpace(it.Id) && byId.TryGetValue(it.Id, out var cur))
        {
          if (string.Equals(cur.Hash ?? "", it.Hash ?? "", StringComparison.OrdinalIgnoreCase))
          { skip++; toUpsert.Add(it); continue; }
          it.Id = Guid.NewGuid().ToString("D"); ins++; toUpsert.Add(it);
          if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("m/" + it.Hash);
          continue;
        }
        if (!string.IsNullOrWhiteSpace(it.Hash) && byHash.ContainsKey(it.Hash)) { skip++; continue; }
        ins++; toUpsert.Add(it);
        if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("m/" + it.Hash);
      }

      var res = target.UpsertMetadata(toUpsert);
      return (res.inserted, res.updated, res.skipped, newHashes);
    }

    public static (int ins, int upd, int skip, List<string> newHashes)
      MergeAxis(IEnumerable<ProgesiAxisVariableDto> src, IProgesiStore target)
    {
      var newHashes = new List<string>();
      var existing = target.GetAllAxisVariables();
      var byId = existing.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
      var byHash = existing.GroupBy(x => x.Hash ?? "")
                           .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

      var toUpsert = new List<ProgesiAxisVariableDto>(); int ins = 0, skip = 0;
      foreach (var it in src)
      {
        if (!string.IsNullOrWhiteSpace(it.Id) && byId.TryGetValue(it.Id, out var cur))
        {
          if (string.Equals(cur.Hash ?? "", it.Hash ?? "", StringComparison.OrdinalIgnoreCase))
          { skip++; toUpsert.Add(it); continue; }
          it.Id = Guid.NewGuid().ToString("D"); ins++; toUpsert.Add(it);
          if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("ax/" + it.Hash);
          continue;
        }
        if (!string.IsNullOrWhiteSpace(it.Hash) && byHash.ContainsKey(it.Hash)) { skip++; continue; }
        ins++; toUpsert.Add(it);
        if (!string.IsNullOrWhiteSpace(it.Hash)) newHashes.Add("ax/" + it.Hash);
      }

      var res = target.UpsertAxisVariables(toUpsert);
      return (res.inserted, res.updated, res.skipped, newHashes);
    }
  }
}
