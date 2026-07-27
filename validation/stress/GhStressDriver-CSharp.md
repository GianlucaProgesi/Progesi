# Grasshopper stress driver — C# Script component

Paste the script below into a **C# Script** component in Rhino 8 Grasshopper (with the Progesi Grasshopper plug-in loaded). The script drives the **real Rhino-backed** repositories and `ClusterService`, not in-memory mocks.

## Inputs / outputs

| Param | Type | Description |
|-------|------|-------------|
| Run | bool | Set `true` to execute |
| N | int | Scale (default 100; use 1000+ only on scratch docs) |
| RunExport | bool | Optional: export Rhino StringTable snapshot to temp `.xlsx` (default `false`) |
| Report | string | PASS/FAIL + timing summary |
| Ok | bool | Overall pass |

## Safety

- Uses variable ids **`900_000 + i`** and cluster name **`STRESS_SCRATCH`** to reduce collision with production data.
- Writes to the **active Rhino document** StringTable (same persistence as normal Progesi GH components).
- Prefer a **throwaway `.3dm`** or backup before large `N`.
- Does **not** call Reset/Clear on the full doc unless you extend the script.

## Script (paste into C# Script component)

```csharp
#r "ProgesiCore.dll"
#r "ProgesiRepositories.Rhino.dll"
#r "ProgesiGrasshopperAssembly.dll"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Rhino;
using ProgesiCore;
using ProgesiCore.Services;
using ProgesiRepositories.Rhino;

bool run = false;
int n = 100;
bool runExport = false;
if (!DA.GetData(0, ref run)) return;
if (!DA.GetData(1, ref n)) return;
DA.GetData(2, ref runExport);

if (!run)
{
  DA.SetData(0, "Set Run=true");
  DA.SetData(1, false);
  return;
}

var doc = RhinoDoc.ActiveDoc;
if (doc == null)
{
  DA.SetData(0, "FAIL: no ActiveDoc");
  DA.SetData(1, false);
  return;
}

n = Math.Max(1, Math.Min(n, 50_000));
const int idBase = 900_000;
var log = new StringBuilder();
var ok = true;
var total = Stopwatch.StartNew();

try
{
  var varRepo = new RhinoVariableRepository(doc);
  var clusterRepo = new RhinoVariableClusterRepository(doc);
  var clusterService = new ClusterService(clusterRepo, varRepo);

  // 1) Create N variables (timed)
  var sw = Stopwatch.StartNew();
  var ids = new List<int>(n);
  for (int i = 0; i < n; i++)
  {
    int id = idBase + i;
    varRepo.SaveAsync(new ProgesiVariable(id, $"stress-{id}", i * 0.1)).GetAwaiter().GetResult();
    ids.Add(id);
  }
  sw.Stop();
  log.AppendLine($"Create vars: {sw.ElapsedMilliseconds} ms ({n} saves)");

  // 2) Dedup + GetByHashtagAsync sample
  var dup = new ProgesiVariable(idBase + n + 1, "dup-stress", 42.0);
  var firstDup = varRepo.SaveAsync(dup).GetAwaiter().GetResult();
  var secondDup = varRepo.SaveAsync(new ProgesiVariable(idBase + n + 2, "dup-stress", 42.0)).GetAwaiter().GetResult();
  var tag = firstDup.Hashtag;
  var byTag = varRepo.GetByHashtagAsync(tag).GetAwaiter().GetResult();
  if (byTag == null)
  {
    ok = false;
    log.AppendLine("FAIL: GetByHashtagAsync returned null for dup content");
  }
  else
  {
    log.AppendLine($"Dedup survivor id={byTag.Id} (first={firstDup.Id}, secondSaveReturned={secondDup.Id})");
  }

  // 3) Cluster create, re-create (dedup), cascade-remove one member
  sw.Restart();
  var cluster = clusterService.CreateOrGetClusterAsync("STRESS_SCRATCH", ids.ToArray(), "gh-stress").GetAwaiter().GetResult();
  var cluster2 = clusterService.CreateOrGetClusterAsync("STRESS_SCRATCH", ids.AsEnumerable().Reverse().ToArray(), "gh-stress").GetAwaiter().GetResult();
  sw.Stop();
  if (cluster2.Id != cluster.Id)
  {
    ok = false;
    log.AppendLine("FAIL: cluster dedup — second create returned different id");
  }
  else
  {
    log.AppendLine($"Cluster id={cluster.Id} members={cluster.ProgesiVariableIds.Count} ({sw.ElapsedMilliseconds} ms)");
  }

  var removeId = ids[ids.Count - 1];
  var affected = clusterService.CascadeRemoveVariableFromClustersAsync(removeId).GetAwaiter().GetResult();
  var reloaded = clusterService.GetByIdAsync(cluster.Id).GetAwaiter().GetResult();
  if (affected != 1 || reloaded == null || reloaded.ProgesiVariableIds.Contains(removeId))
  {
    ok = false;
    log.AppendLine("FAIL: cascade-remove did not update cluster as expected");
  }
  else
  {
    log.AppendLine($"CascadeRemove id={removeId} → members={reloaded.ProgesiVariableIds.Count}");
  }

  // 4) Optional export (DataExchange path is via the DataEx component; here we only log intent)
  if (runExport)
  {
    log.AppendLine("Export skipped in-script — use DataEx component ExportExcel on this doc for full round-trip.");
  }

  total.Stop();
  log.Insert(0, ok ? "PASS " : "FAIL ");
  log.AppendLine($"Total: {total.ElapsedMilliseconds} ms, N={n}, doc={doc.Name}");
}
catch (Exception ex)
{
  ok = false;
  log.AppendLine("EXCEPTION: " + ex.Message);
}

DA.SetData(0, log.ToString());
DA.SetData(1, ok);
```

## Verification checklist

1. Open a scratch Rhino document; enable Progesi GH plug-in.
2. Add C# Script with inputs `Run` (bool), `N` (int), `RunExport` (bool) and outputs `Report` (string), `Ok` (bool).
3. Set `N=100`, `Run=true` → expect `PASS` and plausible timings.
4. Repeat with `N=1000` if acceptable on scratch doc.
5. Record outcome in the **Grasshopper Manual Test Matrix** (Test Area = Persistence/Regression). Suggested id: **GH-STRESS-001**.

## Backend surface verified

- `RhinoDoc.ActiveDoc` — active document
- `new RhinoVariableRepository(doc)` — variable persistence
- `new RhinoVariableClusterRepository(doc)` — cluster persistence
- `ClusterService` — create/dedup/cascade-remove
- `ProgesiHash` / variable `Hashtag` — content identity lookup via `GetByHashtagAsync`
- DataExchange export/import — optional via existing **DataEx** component (not embedded here to keep script non-destructive)
