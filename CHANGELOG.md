# Changelog
Tutte le modifiche rilevanti al plugin Progesi (Grasshopper) sono documentate in questo file.

Il formato segue il modello Keep a Changelog e il versioning e' semantico quando possibile.

---

## [v0.9.0-beta] - 2025-11-17
### Added
- Documentazione completa: `README.md`, `docs/DEPLOY.md`, `docs/TROUBLESHOOTING.md`, `docs/CI.md`.
- Script di packaging: `Make-Releases-Save-Progesi-S2-C2.ps1` per generare lo zip del plugin GH.

### Changed
- Plugin GH semplificato: **ExportEf** e **ImportEf** ora mappano **direttamente** su SQLite (schema EF-compatibile).
- Componente DataEx stabilizzato in modalita' **pure SQLite** (no dipendenze EF a runtime in GH).

### Fixed
- Rimozione definitiva dei fallback EF in-proc e dei relativi errori runtime.
- Nessun caricamento di `EntityFramework.dll`, `System.Data.SQLite.EF6.dll`, `System.Data.SQLite.Linq.dll`, `SQLite.Interop.dll` in GH.

### Notes
- L'uso di EF rimane supportato **fuori** da GH via progetto `Progesi.Data.EF` e tool opzionale `Progesi.EF.Tool`.
- Lo zip di rilascio contiene solo la `.gha` e le dipendenze managed necessarie.

---

## [v0.8.0-beta] - 2025-11-15
### Added
- Import/Export **SQLite** con:
  - Strict/Lenient mode
  - Preview (DryRun)
  - ErrRC (coordinate errori row,col)
  - Log file `*.import.log.txt`

### Changed
- Schema SQLite consolidato:
  - `Metadata`, `Variables`, `Refs`, `VariableDepends`
  - FK ON, nessun UNIQUE su Hash
  - `Variables.MetaId` scritto a NULL se il metadato non esiste

### Fixed
- FK constraint fail in export quando `MetaId` non esisteva nei Metadata.
- Gestione lock su file esistente: in caso di file in uso viene generato un nome con timestamp.

---

## [v0.7.0-beta] - 2025-11-14
### Added
- Integrazione iniziale EF (S2-C/1) con fallback automatico e tool esterno `Progesi.EF.Tool`.
- Script di build/deploy del tool: `Build-Deploy-ProgesiEF-Tool.ps1`.

### Changed
- Rami ExportEf/ImportEf con fallback controllato:
  - se EF in-proc non parte, tenta tool esterno,
  - se il tool non e' disponibile, ricade su SQLite.

### Fixed
- Messaggi di esito uniformati (WHY/TOOL) per diagnosi dei fallback.

---

## [v0.6.0-beta] - 2025-11-12
### Added
- Excel Import/Export (ClosedXML):
  - validazione header con alias JSON
  - Strict/Lenient
  - Preview (DryRun)
  - ErrRC (coordinate errori)
  - log `*.import.log.txt`

### Fixed
- Verifica simultanea degli header mancanti su entrambi gli sheet (Metadata e Variables) prima del processing.

---

## [v0.5.0-beta] - 2025-11-10
### Added
- Componenti base DataEx:
  - ExportExcel / ImportExcel
  - ExportSqlite / ImportSqlite
  - Strutture Warn/Err/Counts uniformi

---

## Upgrade notes

- **Da versioni precedenti alla v0.9.0-beta**  
  Il plugin GH non carica piu' EF/SQLite-EF6 in-proc. Gli act `ExportEf`/`ImportEf`
  usano SQLite come formato di staging (schema invariato).  
  Se hai pipeline che richiedono EF, usa il progetto `Progesi.Data.EF` o il tool `Progesi.EF.Tool`
  al di fuori di GH.

- **Script utili**
  - `Deploy-Progesi-GHA.ps1`: copia la .gha e le dll nella cartella Libraries di Grasshopper.
  - `Make-Releases-Save-Progesi-S2-C2.ps1`: crea lo zip del plugin GH.
  - `Save-Progesi-*.ps1`: script di push per i vari step (branch e tag).

---

## Known issues

- In modalita' Strict, file Excel con header parziali generano errori prima del processing. Usare `Map` alias JSON o passare a Lenient.
- Su file .db generati da export interrotto, l'import segnala "schema not found": rigenerare il DB con `ExportSqlite`.

---

## History (selezione)
- S1-5: Snip component (drag&drop, clipboard), normalizzazione Ref, validazione, dedupe Var/Meta.
- S2-A/B: pipeline SQLite completa (export/import), schema e controlli stabilizzati.

---

[Unreleased]: https://github.com/GianlucaProgesi/Progesi
[v0.9.0-beta]: https://github.com/GianlucaProgesi/Progesi/releases/tag/v0.9.0-beta
[v0.8.0-beta]: https://github.com/GianlucaProgesi/Progesi/releases/tag/v0.8.0-beta
[v0.7.0-beta]: https://github.com/GianlucaProgesi/Progesi/releases/tag/v0.7.0-beta
[v0.6.0-beta]: https://github.com/GianlucaProgesi/Progesi/releases/tag/v0.6.0-beta
[v0.5.0-beta]: https://github.com/GianlucaProgesi/Progesi/releases/tag/v0.5.0-beta
