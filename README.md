# Progesi – DataEx (Grasshopper)

DataEx è il componente GH per **Import/Export** dei dati Progesi:
- **Excel** (`ExportExcel` / `ImportExcel`) – ClosedXML
- **SQLite** (`ExportSqlite` / `ImportSqlite`) – database di staging
- **EF** (`ExportEf` / `ImportEf`) – *alias* di SQLite nel plugin GH (S2-C/2: pipeline “pure SQLite”)

> **Nota EF (S2-C/2):** Il plugin GH **non carica** più EF/SQLite-EF6 in-proc. Gli act `ExportEf/ImportEf`
> mappano direttamente su **SQLite** (stesso schema). L’uso di EF è riservato agli strumenti esterni
> (`Progesi.Data.EF`, `Progesi.EF.Tool`) o ad altre app.

---

## Input/Output del componente

### Inputs
| Nome | Sigla | Tipo | Default | Descrizione |
| --- | --- | --- | --- | --- |
| Run | Run | bool | `false` | Esegue l’azione quando *true* |
| Action | Act | text | `ExportExcel` | Una tra: `ExportExcel` `ImportExcel` `ExportSqlite` `ImportSqlite` `ExportEf` `ImportEf` |
| Path | Path | text | `""` | Percorso file `.xlsx` o `.db` (se vuoto, usa Desktop con nome di default) |
| Overwrite | Ovr | bool | `true` | In export, sovrascrive il file se esiste |
| Mode | Mode | text | `Lenient` | Solo import: `Strict` / `Lenient` |
| Fail on error | Fail | bool | `false` | Stop import se errori ≥ MaxErr |
| Max errors | MaxErr | int | `1000` | Soglia errori per stop |
| Aliases map | Map | text | `""` | JSON opzionale per alias header Excel |
| Preview | Dry | bool | `false` | Import **senza scrivere** (validazione + log) |

### Outputs
| Nome | Sigla | Tipo | Descrizione |
| --- | --- | --- | --- |
| Info | Info | string | Esito sintetico (es. `OK ExportSqlite → …`) |
| Path | Path | string | File interessato (o log) |
| Warnings | Warn | tree\<string\> | Avvisi su Meta (branch {0}) e Vars (branch {1}) |
| Errors | Err | tree\<string\> | Errori su Meta (branch {0}) e Vars (branch {1}) |
| Counts | Counts | tree\<string\> | Riepilogo import: `Meta rows=… ok=… warn=… err=…` e `Vars rows=… ok=… warn=… err=…` |
| Err row/col | ErrRC | tree\<int\> | Coordinate errori `[row, col]` per branch {0}=Meta, {1}=Vars |

---

## Workflow rapidi

### Excel
- **Export**: `Run=TRUE`, `Act="ExportExcel"`, `Path=…\progesi.xlsx` → crea due fogli `ProgesiVariables`, `ProgesiMetadata`.
- **Import**: `Run=TRUE`, `Act="ImportExcel"`, `Mode="Strict"`/`"Lenient"`, `Dry=TRUE/FALSE` → validazione header, mapping alias, log `*.import.log.txt`.

### SQLite
- **Export**: `Run=TRUE`, `Act="ExportSqlite"`, `Path=…\progesi.db` → schema sottostante (vedi sotto).
- **Import**: `Run=TRUE`, `Act="ImportSqlite"`, opzioni come Excel, stesse semantiche.

### EF (*alias SQLite inside GH*)
- **ExportEf/ImportEf**: identici ai comandi SQLite (Info mostra prefisso `[DB:SQLite] …`).

---

## Schema SQLite (staging)

```sql
CREATE TABLE IF NOT EXISTS Metadata (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  By           TEXT,
  Description  TEXT,
  LM           TEXT
);
CREATE TABLE IF NOT EXISTS Variables (
  Id           INTEGER PRIMARY KEY,
  Hash         TEXT NOT NULL,
  Name         TEXT NOT NULL,
  Value        TEXT,
  ValC         TEXT,
  MetaId       INTEGER NULL,
  Assumption   INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE SET NULL
);
CREATE TABLE IF NOT EXISTS Refs (
  MetaId       INTEGER NOT NULL,
  Ref          TEXT NOT NULL,
  PRIMARY KEY (MetaId, Ref),
  FOREIGN KEY (MetaId) REFERENCES Metadata(Id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS VariableDepends (
  VarId        INTEGER NOT NULL,
  DepId        INTEGER NOT NULL,
  PRIMARY KEY (VarId, DepId),
  FOREIGN KEY (VarId) REFERENCES Variables(Id) ON DELETE CASCADE
);
