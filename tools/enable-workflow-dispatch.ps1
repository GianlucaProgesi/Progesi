<#
.SYNOPSIS
Aggiunge "workflow_dispatch:" ai workflow YAML in .github/workflows se manca.
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
  if ($txt -match 'workflow_dispatch\s*:') {
    Write-Verbose "Già presente: $($_.Name)"
    return
  }
  if ($txt -match '^\s*on\s*:\s*$' -im) {
    # Inserisci subito dopo "on:"
    $txt = $txt -replace '(^\s*on\s*:\s*$)','`$1`r`n  workflow_dispatch:'
  } elseif ($txt -match '^\s*on\s*:\s*[\r\n]') {
    # Caso "on:" seguito da eventi: aggiungi una riga
    $txt = $txt -replace '(^\s*on\s*:\s*[\r\n])','$1  workflow_dispatch:' + "`r`n"
  } else {
    # Non c'è "on:" → prepend standard header
    $txt = "on:`r`n  workflow_dispatch:`r`n" + $txt
  }
  Set-Content -LiteralPath $p -Value $txt -Encoding UTF8
  Write-Host "Aggiunto workflow_dispatch -> $($_.Name)"
}
