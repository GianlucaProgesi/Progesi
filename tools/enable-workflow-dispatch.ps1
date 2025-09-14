<#
.SYNOPSIS
Aggiunge "workflow_dispatch:" ai workflow YAML in .github/workflows se manca.
Gestisce i casi comuni "on:" su riga propria o con eventi multilinea.
#>
[CmdletBinding()]
param()

$dir = Join-Path (Get-Location) ".github\workflows"
if (-not (Test-Path $dir)) {
  Write-Warning "Cartella $dir non trovata."
  exit 0
}

Get-ChildItem -Path $dir -File -Include *.yml,*.yaml | ForEach-Object {
  $p = $_.FullName
  $txt = Get-Content -LiteralPath $p -Raw

  if ($txt -match '(?im)^\s*workflow_dispatch\s*:') {
    Write-Host "OK    $($_.Name)"
    return
  }

  if ($txt -match '(?im)^\s*on\s*:\s*$') {
    # "on:" da solo
    $txt = $txt -replace '(?im)^\s*on\s*:\s*$', "on:`r`n  workflow_dispatch:`r`n"
  }
  elseif ($txt -match '(?im)^\s*on\s*:\s*[\r\n]+') {
    # "on:" seguito da eventi su righe successive
    $txt = $txt -replace '(?im)^\s*on\s*:\s*', "on:`r`n  workflow_dispatch:`r`n"
  }
  else {
    # Nessun "on:" classico trovato: come fallback, preprendo un blocco on:
    # (evita casi esotici; se necessario si ritocca a mano)
    $txt = "on:`r`n  workflow_dispatch:`r`n$txt"
  }

  Set-Content -LiteralPath $p -Value $txt -Encoding UTF8
  Write-Host "ADDED workflow_dispatch -> $($_.Name)"
}
