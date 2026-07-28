# DataExchange manual round-trip validation (R2-C.0)

Primary safety net for the **live** Grasshopper DataEx path before R2-C.1 extracts the exchange library. CI covers the Excel **read contract**; this procedure validates Export → Import through Rhino persistence.

## Preconditions

- Rhino 8 + Grasshopper with the **Progesi** plug-in loaded
- Branch under test checked out and built (`ProgesiGrasshopperAssembly` deployed to GH Components folder per project README)
- Scratch `.3dm` files only — do not use production models

## Known test model (build in GH before export)

Create the following in Rhino via Progesi GH components (VarDef/VarIn, MetadataIn, ClusterDef):

| Kind | Id / name | Content |
|------|-----------|---------|
| Variable 1 | Id 1, `Span` | primitive `12.5`, metadata link → meta 1 |
| Variable 2 | Id 2, `Width` | primitive `3`, assumption=true, depends on 1, metadata → 1+2 |
| Variable 3 | Id 3, `BeamCurve` | **Rhino geometry** (e.g. Line/Brep), metadata → 2, depends 1+2 |
| Metadata 1 | Id 1 | By `eng`, description, refs `https://example.com/a`, `https://example.com/b` |
| Metadata 2 | Id 2 | By `qa`, description, ref `https://example.com/c`; include **one snip** if MetadataIn supports it |
| Cluster 1 | Id 1, `LoadCase` | members variables 1, 2, 3 |

Record domain hashtags from VarOut / MetadataOut / ClusterOut for post-import comparison.

---

## Procedure A — Excel round-trip

1. Open scratch document **A.3dm**; build the known model above.
2. Add **DataEx** component (`ProgesiDataExchangeComponent`):
   - Action = `ExportExcel`
   - Path = e.g. `%USERPROFILE%\Desktop\r2c0-roundtrip-export.xlsx`
   - Overwrite = true
3. Run export; confirm success message lists Vars/Meta/Clusters/ObjectChunks > 0 for geometry var.
4. Open a **new** scratch document **B.3dm** (empty Progesi state).
5. DataEx:
   - Action = `ImportExcel`
   - Path = exported file
   - Mode = `Strict` (first pass)
   - DryRun = false
6. Verify in **B.3dm**:
   - [ ] All 3 variables present with correct Name/Value
   - [ ] No `@UNSUPPORTED` values on import (geometry var uses `@OBJECT:` + object sheet)
   - [ ] Geometry reconstructs (preview/select var 3 in GH)
   - [ ] Metadata 1+2: By, Description, Refs intact
   - [ ] Cluster 1: members `{1,2,3}`; cascade/remove not required for this test
   - [ ] Hashtags match pre-export domain digests (VarOut/MetadataOut/ClusterOut)
7. Optional: repeat with Mode = `Lenient` on a deliberately noisy copy (record warnings only).

---

## Procedure B — SQLite round-trip

1. From document **A.3dm** (or rebuild model):
   - Action = `ExportSqlite`
   - Path = e.g. `%USERPROFILE%\Desktop\r2c0-roundtrip.sqlite`
2. New document **C.3dm**:
   - Action = `ImportSqlite`
   - Path = exported `.sqlite`
   - Mode = `Strict`
3. Verify same checklist as Procedure A (variables, metadata, refs, depends, cluster, geometry).
4. Optional schema spot-check (DB Browser for SQLite):
   - Tables: `Metadata`, `Variables`, `Refs`, `VariableDepends`, `Clusters`, `ClusterVariables`
   - FK pragma on; row counts match model

---

## Procedure C — EF route (optional)

The live GH **ExportEf / ImportEf** path uses `Progesi.EF.Tool.exe` as an external step. Document only if used:

1. ExportEf → SQLite intermediate (per component info string)
2. Run EF.Tool per project docs
3. Import back via ImportEf or ImportSqlite fallback

Record tool version and command line in evidence. Skip if EF.Tool is not configured on the test machine.

---

## Manual Test Matrix — DX-ROUNDTRIP-001

| Field | Value |
|-------|-------|
| **Test ID** | DX-ROUNDTRIP-001 |
| **Test Area** | DataExchange / Regression |
| **Title** | Live GH Export → new doc Import (Excel + SQLite) |
| **Preconditions** | Progesi GH plug-in loaded; scratch `.3dm`; known model built |
| **Steps** | Procedure A + B above |
| **Expected** | All objects/values/hashtags round-trip; geometry reconstructed; cluster members intact; no `@UNSUPPORTED` on import |
| **Evidence** | Export info strings; import counts; screenshot or text dump of VarOut/MetadataOut/ClusterOut; attach `.xlsx` / `.sqlite` |
| **Environment** | *(fill)* Rhino `___` / GH `___` / OS / branch `@ commit |
| **Result** | Passed / Failed / Blocked |
| **Tester** | *(fill)* |
| **Date** | *(fill)* |

### Failure escalation

- If Excel passes but SQLite fails (or vice versa), note which path and attach the log tree from DataEx.
- If geometry fails reassembly, check `ProgesiVariableObjects` sheet chunk count in export.
- Do **not** mark R2-C.1 ready until DX-ROUNDTRIP-001 is **Passed** on the release candidate branch.

---

## Related automated coverage (R2-C.0)

- `GhExcelFormatRoundTripTests` — writes canonical sheets via ClosedXML, reads via `Progesi.GhExcelReadContract`
- Golden fixture: `validation/dataexchange/golden/r2c0-canonical-model.xlsx`

Automated tests do **not** replace this manual procedure for the Rhino-backed live path.
