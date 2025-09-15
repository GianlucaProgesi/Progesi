# tools/enable-workflow-dispatch.ps1
<#
.SYNOPSIS
  Aggiunge (idempotente) 'workflow_dispatch' a tutti i workflow in .github/workflows.

.DESCRIPTION
  - Se esiste già, non fa nulla.
  - Supporta:
      on: [push, pull_request]
      on:
        push:
        pull_request:
  - Se non c’è 'on:' lo inserisce da zero.
  - Per default non crea backup (.bak). Usa -CreateBackup per salvarli.

.EXAMPLES
  pwsh -File .\tools\enable-workflow-dispatch.ps1
  pwsh -File .\tools\enable-workflow-dispatch.ps1 -CreateBackup
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
  [string]$WorkflowsDir = ".github/workflows",
  [switch]$CreateBackup
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WorkflowsDir)) {
  Write-Error "Cartella non trovata: $WorkflowsDir"
  exit 1
}

$files = Get-ChildItem -Path $WorkflowsDir -Include *.yml,*.yaml -File -Recurse -ErrorAction SilentlyContinue
if (-not $files) {
  Write-Host "Nessun file workflow trovato in $WorkflowsDir."
  exit 0
}

function Add-Dispatch {
  param([string]$text)

  # già presente?
  if ($text -match '(?m)^\s*workflow_dispatch\s*:') {
    return $text, $false
  }

  # Caso 1: on: [ ... ]  (forma array)
  if ($text -match '(?ms)^\s*on\s*:\s*\[(.*?)\]') {
    $inside = $Matches[1]
    $events = $inside.Split(',') |
      ForEach-Object { ($_ -replace '["'']','').Trim() } |
      Where-Object { $_ }

    if (-not ($events -contains 'workflow_dispatch')) {
      $events += 'workflow_dispatch'
    }

    # Ricrea in forma mapping (più estendibile) — **FIX** usare $($_) per evitare l'errore con i due punti
    $map = "on:`r`n" + ($events | ForEach-Object { "  $($_):" }) -join "`r`n"
    $new = [regex]::Replace($text,'(?ms)^\s*on\s*:\s*\[.*?\]',$map,1)
    return $new, $true
  }

  # Caso 2: on:  (forma mapping)
  if ($text -match '(?m)^\s*on\s*:\s*$') {
    $new = $text -replace '(?m)^\s*on\s*:\s*$', "on:`r`n  workflow_dispatch:`r`n"
    return $new, $true
  }
  if ($text -match '(?m)^\s*on\s*:\s*.*$') {
    # on: con roba sulla stessa riga → converti in mapping pulito
    $new = $text -replace '(?m)^\s*on\s*:\s*.*$', "on:`r`n  workflow_dispatch:`r`n"
    return $new, $true
  }

  # Caso 3: nessun 'on:' → prepend
  $prepend = "on:`r`n  workflow_dispatch:`r`n"
  return ($prepend + $text), $true
}

$changed = @()

foreach ($f in $files) {
  $raw = Get-Content -Path $f.FullName -Raw -Encoding UTF8
  $newText, $didChange = Add-Dispatch -text $raw

  if ($didChange) {
    if ($PSCmdlet.ShouldProcess($f.FullName,"Add workflow_dispatch")) {
      if ($CreateBackup) {
        Copy-Item -Path $f.FullName -Destination ($f.FullName + ".bak") -Force
      }
      Set-Content -Path $f.FullName -Value $newText -Encoding UTF8 -NoNewline
      $changed += $f.FullName
      Write-Host "[MOD] $($f.FullName)"
    }
  } else {
    Write-Host "[OK ] $($f.FullName) già contiene 'workflow_dispatch'."
  }
}

Write-Host ""
if ($changed.Count -gt 0) {
  Write-Host "File modificati:" -ForegroundColor Cyan
  $changed | ForEach-Object { Write-Host " - $_" }
  Write-Host ""
  Write-Host "Esegui:" -ForegroundColor Yellow
  Write-Host "  git add .github/workflows"
  Write-Host "  git commit -m `"ci: add workflow_dispatch to workflows`""
  Write-Host "  git push"
} else {
  Write-Host "Nessuna modifica necessaria."
}
