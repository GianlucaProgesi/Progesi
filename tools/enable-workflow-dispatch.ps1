<#
.SYNOPSIS
Aggiunge "workflow_dispatch:" ai workflow in .github/workflows se manca.
- Gestisce in automatico il caso con "on:" su riga propria (blocco multilinea).
- Se il workflow usa forme inline (es. "on: [push, pull_request]" o "{...}"), stampa un WARNING e ti dice cosa fare a mano.
#>
[CmdletBinding()]
param()

$dir = Join-Path (Get-Location) ".github\workflows"
if (-not (Test-Path $dir)) {
  Write-Warning "Cartella $dir non trovata."
  exit 0
}

# TROVA i file correttamente (niente -Include senza wildcard)
$files = Get-ChildItem -Path "$dir\*.yml","$dir\*.yaml" -File -ErrorAction SilentlyContinue
if (-not $files) {
  Write-Warning "Nessun file workflow trovato in $dir."
  exit 0
}

foreach ($f in $files) {
  $p = $f.FullName
  $txt = Get-Content -LiteralPath $p -Raw

  if ($txt -match '(?im)^\s*workflow_dispatch\s*:') {
    Write-Host "OK     $($f.Name)"
    continue
  }

  # Caso semplice: "on:" su riga propria -> inserisco subito sotto
  if ($txt -match '(?im)^\s*on\s*:\s*$') {
    $txt = [regex]::Replace($txt,'(?im)^(\s*on\s*:\s*)$',"`$1`r`n  workflow_dispatch:")
    Set-Content -LiteralPath $p -Value $txt -Encoding UTF8
    Write-Host "ADDED  workflow_dispatch -> $($f.Name)  (inserito dopo 'on:')"
    continue
  }

  # Forme inline: avviso e istruzioni manuali (più sicuro)
  if ($txt -match '(?im)^\s*on\s*:\s*\[') {
    Write-Warning "Formato inline array in $($f.Name): cambia
on: [push, pull_request]
in:
on:
  push:
  pull_request:
  workflow_dispatch:
"
    continue
  }
  if ($txt -match '(?im)^\s*on\s*:\s*\w+') {
    Write-Warning "Formato inline scalare in $($f.Name): cambia
on: push
in:
on:
  push:
  workflow_dispatch:
"
    continue
  }
  if ($txt -match '(?im)^\s*on\s*:\s*\{') {
    Write-Warning "Formato inline mappa in $($f.Name): espandi a blocco e aggiungi:
on:
  push: {}
  pull_request: {}
  workflow_dispatch:
"
    continue
  }

  # Fallback prudente
  Write-Warning "Schema 'on:' non riconosciuto in $($f.Name). Apri il file e aggiungi 'workflow_dispatch:' sotto il blocco 'on:'."
}
