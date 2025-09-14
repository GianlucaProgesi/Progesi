# Crea/aggiorna docs/TOOLS.md
New-Item -ItemType Directory -Path .\docs -Force | Out-Null
Set-Content -LiteralPath .\docs\TOOLS.md -Encoding UTF8 -Value @'
# Progesi — Guida agli script in `tools`

Questa guida spiega **cosa fa** ogni script, **come** e **quando** usarlo.

> Convenzioni:
> - Esegui con: `pwsh ./tools/<nome>.ps1`
> - Aggiungi `-Verbose` per log dettagliato
> - Dove supportato, `-WhatIf` fa un **dry-run**

---

## 1) `clean-solution.ps1` — pulizia chirurgica
**Cosa fa**
- Rimuove `bin/`, `obj/`, `.vs/` e quasi tutti i `TestResults/`
- Mantiene `TestResults/**/MergedCoverage/` (a meno che non passi `-RemoveMergedCoverage`)
- Ripulisce cartelle extra in root: `CoverageReport/`, `diagnostics-*`, `run-report-*`

**Uso**
```pwsh
pwsh ./tools/clean-solution.ps1 -WhatIf -Verbose     # Dry-run
pwsh ./tools/clean-solution.ps1 -Verbose -Force      # Esecuzione reale
pwsh ./tools/clean-solution.ps1 -RemoveMergedCoverage
