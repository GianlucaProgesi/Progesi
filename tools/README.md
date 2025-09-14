# tools – Guida rapida

> Obiettivo: pochi script, semplici, ripetibili. Tutti eseguibili con PowerShell 7+ o Windows PowerShell 5.1.

## coverage.ps1
**Cosa:** esegue i test dei due progetti, produce i Cobertura, fa merge, applica gate (soglia), genera report HTML.  
**Come:**  
```pwsh
pwsh ./tools/coverage.ps1 -RunTests
pwsh ./tools/coverage.ps1 -MinLine 79               # applica solo il gate sulla merge esistente
