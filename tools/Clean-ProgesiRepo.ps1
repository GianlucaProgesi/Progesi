[CmdletBinding()]
param(
  [switch]$Apply,            # senza -Apply fa DRY-RUN
  [switch]$VerboseScan,      # mostra elenco completo di ciò che analizza
  [string[]]$ExtraRemove     # percorsi aggiuntivi da rimuovere
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath ".").Path
Write-Host ("Progesi clean @ {0}" -f $root) -ForegroundColor Cyan
Write-Host ("Mode: {0}" -f ($(if ($Apply) { "APPLY" } else { "DRY-RUN" }))) `
  -ForegroundColor ($(if ($Apply) { "Yellow" } else { "DarkYellow" }))

# 1) Pattern cartelle/file “junk”
$junkDirs = @(
  'bin','obj','TestResults','coverage-html','artifacts','nupkg',
  '.vs','.idea','.ionide','packages','.cache','.sonarqube','.scannerwork'
)
$junkFiles = @('*.user','*.suo','*.orig','*.tmp','*.log','*.DS_Store','Thumbs.db')

# 2) Rileva workflow YAML sospetti (placeholder espliciti)
$suspicious = @()
$workflowDir = Join-Path $root ".github\workflows"
if (Test-Path $workflowDir) {
  Get-ChildItem $workflowDir -Filter *.yml -Recurse -File | ForEach-Object {
    $c = Get-Content -LiteralPath $_.FullName -Raw -ErrorAction SilentlyContinue
    if ($c -match '<incolla' -or $c -match 'PLACEHOLDER' -or $c -match 'REPLACE ME') {
      $suspicious += $_.FullName
    }
  }
}

# 3) Costruisce la lista rimovibili
$toRemove = New-Object System.Collections.Generic.List[string]

# Cartelle
Get-ChildItem -LiteralPath $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
  Where-Object { $junkDirs -contains $_.Name } |
  ForEach-Object { $toRemove.Add($_.FullName) }

# File
foreach ($pattern in $junkFiles) {
  Get-ChildItem -LiteralPath $root -File -Recurse -Force -ErrorAction SilentlyContinue -Filter $pattern |
    ForEach-Object { $toRemove.Add($_.FullName) }
}

# Extra
if ($ExtraRemove) {
  foreach ($p in $ExtraRemove) {
    $full = Join-Path $root $p
    if (Test-Path $full) { $toRemove.Add($full) }
  }
}

# Dedup e ordine
$toRemove = $toRemove | Sort-Object -Unique

Write-Host "`n== Candidate removals ==" -ForegroundColor Cyan
if ($toRemove.Count -eq 0) { Write-Host "(none)" } else { $toRemove | ForEach-Object { Write-Host $_ } }

if ($suspicious.Count -gt 0) {
  Write-Host "`n== Suspicious workflow files (manual review) ==" -ForegroundColor Yellow
  $suspicious | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}

if ($VerboseScan -and (Test-Path $workflowDir)) {
  Write-Host "`n== YAML files under .github/workflows ==" -ForegroundColor DarkCyan
  Get-ChildItem $workflowDir -Filter *.yml -File | Select-Object -ExpandProperty FullName
}

if ($Apply) {
  Write-Host "`nRemoving..." -ForegroundColor Red
  foreach ($p in $toRemove) {
    try {
      if (Test-Path $p -PathType Container) {
        Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Stop
      } elseif (Test-Path $p -PathType Leaf) {
        Remove-Item -LiteralPath $p -Force -ErrorAction Stop
      }
    } catch {
      Write-Warning ("Failed to remove {0}: {1}" -f $p, $_.Exception.Message)
    }
  }
  Write-Host "Done."
} else {
  Write-Host "`nDRY-RUN complete. Re-run with -Apply to actually delete." -ForegroundColor DarkYellow
}
