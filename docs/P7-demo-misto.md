# P7 · Demo mista (Metadata + Variables)

Questa demo mostra i componenti `ProgesiMetadataIn/Out` e `ProgesiVariableIn/Out` in modalità **LIVE** (SQLite).

## Prerequisiti
- DB SQLite preparato (es. `tests\P6-live\progesi_p6.db`)
- Live attivo:
  ```powershell
  .\tools\Set-ProgesiMock.ps1 -Off
  .\tools\Set-ProgesiLive.ps1 -On -DatabasePath ".\tests\P6-live\progesi_p6.db"
