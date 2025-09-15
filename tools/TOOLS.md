# Tools

## coverage.ps1
- **Cosa fa**: esegue i test, produce Cobertura + TextSummary, merge multi-progetto e HTML.
- **Uso**:
  - `pwsh ./tools/coverage.ps1 -RunTests`
  - `pwsh ./tools/coverage.ps1 -MinLine 79`
- **Quando usarlo**: in CI e localmente prima di fare PR/importanti refactor.

## coverage-hotspots.ps1
- **Cosa fa**: estrae i “file caldi” (meno coperti) da `Cobertura.xml` e scrive lo Step Summary.
- **Uso**: `pwsh ./tools/coverage-hotspots.ps1 -Cobertura TestResults/MergedCoverage/Cobertura.xml -Top 20`
- **Quando**: subito dopo `coverage.ps1`, per guidare dove scrivere nuovi test.

## enable-workflow-dispatch.ps1
- **Cosa fa**: aggiunge in modo idempotente `workflow_dispatch:` a tutti i workflow.
- **Uso**: `pwsh ./tools/enable-workflow-dispatch.ps1 [-WhatIf]`
- **Quando**: quando aggiungi nuovi workflow o normalizzi quelli esistenti.

## Run-All.ps1
- **Cosa fa**: orchestration locale (test + coverage + check).
- **Uso**: `pwsh ./tools/Run-All.ps1 [-RemoteOnly]`
- **Quando**: smoke-test locale rapido prima del push.

### `audit-repo.ps1`
- **Cosa fa:** genera un audit di workflows e branch remoti (merge + anzianità) e propone candidati all’archiviazione/cancellazione.
- **Uso rapido:** `pwsh ./tools/audit-repo.ps1`
- **Output:** file CSV/MD in `out/audit-YYYYMMDD-HHMMSS/`:
  - `workflows.csv` / `workflows.md`
  - `branches.csv` e `branches-to-delete.txt`
- **Quando usarlo:** prima delle attività di “housekeeping” del repo.
