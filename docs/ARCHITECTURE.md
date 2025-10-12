# Architettura – Progesi

## Progetti
- **ProgesiCore** – modelli dominio, util, guard
- **ProgesiDomainServices** – servizi dominio
- **Repositories** – Sqlite/Rhino/InMemory
- **ProgesiDataExchange** – bridge Excel/SQLite ↔ Rhino
- **ProgesiGrasshopperAssembly** – componenti GH (DataX, Snip, RefCheck, …)
- **ProgesiGrasshopperBrowsers** – browser read-only (Variables/Metadata)
- **tools** – DbMaint, Smoke, scripts
- **tests** – xUnit (Core, Sqlite)

## Flussi principali
- Rhino Store ⟷ Sqlite/Xlsx (DataExchangeRunner)
- Snip/Ref – SnipHelpers + Snip/RefCheck
- Browser – SELECT read-only + export CSV

## Dipendenze pin (soft)
- Microsoft.Data.Sqlite **9.0.9**
- SQLitePCLRaw.* **2.1.10**