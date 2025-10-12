
### `docs/ARCHITECTURE.md` (scheletro)
```markdown
# Architettura – Progesi

## Soluzione / Progetti
- **ProgesiCore**: Domain models, guard, util.
- **ProgesiDomainServices**: servizi dominio.
- **ProgesiRepositories.Sqlite/Rhino/InMemory**: accesso dati.
- **ProgesiDataExchange**: bridge Excel/SQLite ↔ Rhino store.
- **ProgesiGrasshopperAssembly**: componenti GH (DataX, Snip, RefCheck, …).
- **ProgesiGrasshopperBrowsers**: browser read-only (Variables/Metadata).
- **tools**: utilità (DbMaint, Smoke, scripts …).
- **tests**: test xUnit (Core, Sqlite).

## Flussi principali
- RHINO STORE ⟷ Sqlite/Xlsx (DataExchangeRunner).
- Snip/Ref: `SnipHelpers` + `ProgesiSnipComponent` + `ProgesiRefCheckComponent`.
- Browser: SELECT read-only + export CSV.

## Dipendenze chiave (pinned)
- Microsoft.Data.Sqlite 9.0.9
- SQLitePCLRaw.* 2.1.10
- RhinoCommon/Grasshopper (NuGet in CI, install locale in dev)
