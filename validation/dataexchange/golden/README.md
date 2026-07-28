# R2-C.0 golden Excel fixture

Canonical live DataExchange workbook used as the post–R2-C.1 extraction diff reference.

## Fixed model (`r2c0-canonical-model.xlsx`)

| Entity | Count | Notes |
|--------|------:|-------|
| Variables | 3 | Id 1–3: primitives + one geometry via `@OBJECT:` + `ProgesiVariableObjects` chunks |
| Metadata | 2 | Id 1–2 with pipe-separated refs |
| Clusters | 1 | Id 1, members `{1,2,3}` |

### Variables

| Id | Name | Value | MetaId | Depends | Assumption | Geometry |
|---:|------|-------|--------|---------|:----------:|----------|
| 1 | Span | `12.5` | 1 | — | no | — |
| 2 | Width | `3` | 1,2 | 1 | yes | — |
| 3 | BeamCurve | `@OBJECT:Rhino.Geometry.LineCurve` | 2 | 1,2 | no | payload length **> 30 000** (2 chunks) |

### Metadata

| Id | By | Description | Refs |
|---:|----|-------------|------|
| 1 | eng | Primary metadata | `https://example.com/a\|https://example.com/b` |
| 2 | qa | Secondary metadata | `https://example.com/c` |

### Cluster

| Id | Name | Description | VariableIds |
|---:|------|-------------|-------------|
| 1 | LoadCase | Set A | 1,2,3 |

## Sheet contract (live GH export)

| Sheet | Columns |
|-------|---------|
| `ProgesiVariables` | Id, Hash, Name, Value, ValC, MetaId, Depends, Assumption |
| `ProgesiMetadata` | Id, Hash, By, Description, Refs, LM |
| `ProgesiClusters` | Id, Hash, Name, Description, VariableIds |
| `ProgesiVariableObjects` | VarId, ChunkIndex, ChunkCount, ObjectType, Payload |

## How the fixture was generated

Programmatically via `CanonicalExchangeWorkbookBuilder` in `tests/Progesi.GhExcelReadContract.Tests` (same code path as the format round-trip tests):

```powershell
$env:PROGESI_WRITE_GOLDEN = "1"
dotnet test -c Release tests/Progesi.GhExcelReadContract.Tests/Progesi.GhExcelReadContract.Tests.csproj --filter "FullyQualifiedName~Generate_Golden_Fixture"
```

Or rebuild from the fixed model in `Support/CanonicalExchangeModel.CreateFixedModel()`.

## How to diff after R2-C.1

1. Export the same fixed model from the extracted library (or GH DataEx) to a scratch `.xlsx`.
2. Compare sheet names and column headers first (must match the table above).
3. Diff cell values row-by-row on each sheet (order-stable for Variables/Metadata/Clusters; chunk rows for `ProgesiVariableObjects` may appear in chunk-index order).
4. For geometry variable Id **3**, confirm:
   - Value cell = `@OBJECT:Rhino.Geometry.LineCurve`
   - Reassembled payload length = **30 456** characters (`DefaultMaxChunkLength + 456`)
5. Automated guard: `GhExcelFormatRoundTripTests.GoldenFixture_Reads_Back_Canonical_Model` reads this file via `GhExcelWorkbookReader`.

Acceptance for R2-C.1: extracted export is **cell-equivalent** to this fixture for the fixed model (hashes in the fixture are literal placeholders; live export may emit domain hashtags — document any intentional hash column change separately).

## Canonical relational SQLite schema (live GH ExportSqlite / ImportSqlite)

Reference schema written by `ProgesiDataExchangeComponent.ExportSqlite` (INTEGER primary keys, FK enforcement `PRAGMA foreign_keys=ON`):

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

CREATE TABLE IF NOT EXISTS Clusters (
  Id          INTEGER PRIMARY KEY,
  Hash        TEXT NOT NULL,
  Name        TEXT NOT NULL,
  Description TEXT
);

CREATE TABLE IF NOT EXISTS ClusterVariables (
  ClusterId   INTEGER NOT NULL,
  VarId       INTEGER NOT NULL,
  PRIMARY KEY (ClusterId, VarId),
  FOREIGN KEY (ClusterId) REFERENCES Clusters(Id) ON DELETE CASCADE,
  FOREIGN KEY (VarId)     REFERENCES Variables(Id) ON DELETE CASCADE
);
```

Notes:
- Normalized refs live in `Refs`; metadata scalar fields in `Metadata`.
- Variable dependencies are rows in `VariableDepends` (not a CSV column).
- Cluster membership is `ClusterVariables` (not a CSV column on `Clusters`).
- A committed `.sqlite` golden is optional; this DDL is the canonical reference for R2-C.1 parity checks.
