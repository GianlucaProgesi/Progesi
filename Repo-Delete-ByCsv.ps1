<#
SYNOPSIS
  Elimina in sicurezza gli elementi marcati come DELETE nel CSV generato e,
  su richiesta, fa anche una pulizia per pattern (bin/obj/.vs/out/TestResults/…).

USO TIPICO
  .\Repo-Delete-ByCsv.ps1 -DeleteCsv .\analysis\repo_delete_candidates.csv -HeuristicFallback -DryRun
  .\Repo-Delete-ByCsv.ps1 -DeleteCsv .\analysis\repo_delete_candidates.csv -HeuristicFallback -NoConfirm -GitUntrack
#>

Param(
  [string]$Root = ".",
  [string]$DeleteCsv,
  [switch]$HeuristicFallback,
  [switch]$DryRun,
  [switch]$NoConfirm,
  [switch]$KeepOut,
  [switch]$GitUntrack
)

$ErrorActionPreference = "Stop"

function Write-Gray($m){Write-Host $m -ForegroundColor DarkGray}
function Write-Green($m){Write-Host $m -ForegroundColor Green}
function Write-Yellow($m){Write-Warning $m}
function Write-Red($m){Write-Host $m -ForegroundColor Red}

Push-Location $Root

# Info Git (se presente)
$inGit = $false
try { git rev-parse --is-inside-work-tree *> $null; if ($LASTEXITCODE -eq 0) { $inGit = $true } } catch {}
if ($inGit) {
  $branch = (git rev-parse --abbrev-ref HEAD).Trim()
  Write-Gray "Git branch: $branch"
  $dirty = git status --porcelain
  if ($dirty) { Write-Yellow "ATTENZIONE: working tree non pulita. Consigliato commit/stash PRIMA della pulizia." }
}

function Resolve-CsvPath([string]$rel) {
  if (-not $rel) { return $null }
  $rel = $rel -replace "[/\\]+","/"
  # Candidati: path intero, poi via via senza i segmenti iniziali (per gestire zip con root extra)
  $parts = $rel -split "/"
  $cands = @()
  for ($i=0; $i -lt $parts.Length; $i++) {
    $sub = ($parts[$i..($parts.Length-1)] -join "/")
    $cands += (Join-Path (Get-Location) ($sub -replace "/","\")) 
  }
  foreach ($c in $cands) { if (Test-Path $c) { return $c } }
  return $null
}

function Remove-Target([string]$path) {
  if ($DryRun) { Write-Gray "[dry-run] remove $path"; return $true }
  try {
    if (Test-Path $path -PathType Container) {
      Remove-Item $path -Recurse -Force -ErrorAction Stop
    } elseif (Test-Path $path -PathType Leaf) {
      Remove-Item $path -Force -ErrorAction Stop
    } else {
      return $false
    }
    return $true
  } catch {
    Write-Yellow "Skip: $path -> $($_.Exception.Message)"; return $false
  }
}

$toRemove = New-Object System.Collections.Generic.List[string]

# 1) Da CSV: prendi SOLO righe suggestion=DELETE*
if ($DeleteCsv) {
  if (-not (Test-Path $DeleteCsv)) { Write-Red "CSV non trovato: $DeleteCsv"; Pop-Location; exit 1 }
  $rows = Import-Csv $DeleteCsv
  foreach ($r in $rows) {
    $suggest = ($r.suggestion, $r.Suggestion | Where-Object { $_ })[0]
    if ($suggest -and ($suggest -like "DELETE*")) {
      $p = ($r.path, $r.Path | Where-Object { $_ })[0]
      $resolved = Resolve-CsvPath $p
      if ($resolved) {
        $toRemove.Add($resolved)
      } else {
        Write-Gray "Non trovato dal CSV: $p"
      }
    }
  }
}

# 2) Fallback euristico (opzionale)
if ($HeuristicFallback -or (-not $DeleteCsv)) {
  # Directory note: niente glob **. Usiamo -Recurse e confrontiamo i nomi.
  $dirNames = @('.vs','.idea','.vscode','bin','obj','TestResults','artifacts','.cache','_UpgradeBackup','packages')
  if (-not $KeepOut) { $dirNames += 'out' }

  # Se il repo usa packages.config, NON toccare packages/
  $hasPackagesConfig = @(Get-ChildItem -Recurse -Filter "packages.config" -ErrorAction SilentlyContinue).Count -gt 0
  if ($hasPackagesConfig) {
    $dirNames = $dirNames | Where-Object { $_ -ne 'packages' }
    Write-Yellow "Rilevato packages.config → non elimino 'packages/'"
  }

  Get-ChildItem -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
    $name = $_.Name
    ($dirNames -contains $name) -or ($name -like 'coverage*') -or ($_.FullName -match '\\out\\stage($|\\)')
  } | ForEach-Object { $toRemove.Add($_.FullName) }

  # File spazzatura
  $filePatterns = @('*.user','*.suo','*.tmp','*.log','*.db-wal','*.db-shm','Thumbs.db','.DS_Store')
  foreach ($fp in $filePatterns) {
    Get-ChildItem -Recurse -Include $fp -ErrorAction SilentlyContinue | ForEach-Object { $toRemove.Add($_.FullName) }
  }
}

# De-duplica
$targets = $toRemove | Sort-Object -Unique

if (-not $targets -or $targets.Count -eq 0) {
  Write-Host "Niente da eliminare" -ForegroundColor Yellow
  Pop-Location; exit 0
}

if (-not $NoConfirm) {
  Write-Host "Verranno rimossi $($targets.Count) elementi." -ForegroundColor Cyan
  $answer = Read-Host "Confermi? (Y/N)"
  if ($answer.Trim().ToUpper() -ne 'Y') {
    Write-Host "Annullato."
    Pop-Location; exit 0
  }
}

# Esecuzione
$removed = 0; $missing = 0
foreach ($t in $targets) {
  if (Test-Path $t) {
    if (Remove-Target $t) { $removed++ } else { Write-Yellow "Non rimosso: $t" }
  } else { $missing++ }
}

# Git untrack (se richiesto)
if ($GitUntrack -and $inGit) {
  $untrack = @('.vs','.idea','.vscode','out','bin','obj','TestResults','artifacts','.cache','_UpgradeBackup','packages')
  foreach ($n in $untrack) {
    try { git rm -r --cached $n *> $null } catch {}
  }
  if (-not $DryRun) { git add . *> $null }
  Write-Gray "Git index ripulito (se presente)."
}

Write-Green "✅ Rimozione completata. Removed=$removed, Missing=$missing, DryRun=$DryRun"
Pop-Location
