\
<#
.SYNOPSIS
  Elimina in sicurezza i file/cartelle marcati come DELETE (da CSV) e, in fallback, i pattern classici build/cache/temp.

.DESCRIPTION
  - Se fornisci -DeleteCsv, legge il CSV (colonne: path, suggestion, is_dir opzionale) e rimuove SOLO le righe con suggestion che inizia per 'DELETE'.
  - Se un path del CSV non viene trovato 1:1, prova a risolverlo rimuovendo il primo segmento di percorso (nel caso lo ZIP avesse una root extra).
  - Se non passi -DeleteCsv (o se rimangono elementi non risolti), può applicare una pulizia per pattern (bin/obj/.vs/out/TestResults/coverage*/...).
  - Non tocca il codice sorgente, salvo rimuovere *bin/obj* all'interno di src/ e tests/ (build output).
  - Opzioni: -DryRun (anteprima), -NoConfirm, -HeuristicFallback, -KeepOut.

.PARAMETER Root
  Radice del repository (default: '.').

.PARAMETER DeleteCsv
  Percorso al CSV generato (repo_delete_candidates.csv).

.PARAMETER HeuristicFallback
  Applica anche i pattern standard se rimangono DELETE non risolti o se non viene passato il CSV.

.PARAMETER DryRun
  Non cancella, mostra solamente cosa verrebbe eliminato.

.PARAMETER NoConfirm
  Evita la richiesta di conferma.

.PARAMETER KeepOut
  Mantiene la cartella 'out/' (di default rimuove 'out' e 'out/stage').

.PARAMETER GitUntrack
  Rimuove dal *git index* (se tracciati) i pattern di build/cache (non elimina file versionati di codice).

.EXAMPLE
  .\Repo-Delete-ByCsv.ps1 -DeleteCsv .\analysis\repo_delete_candidates.csv -HeuristicFallback

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

function Write-Gray($msg) { Write-Host $msg -ForegroundColor DarkGray }
function Write-Green($msg) { Write-Host $msg -ForegroundColor Green }
function Write-Yellow($msg) { Write-Warning $msg }
function Write-Red($msg) { Write-Host $msg -ForegroundColor Red }

Push-Location $Root

# Safety: se siamo in una repo git, mostra branch & stato
$inGit = $false
try { git rev-parse --is-inside-work-tree *> $null; if ($LASTEXITCODE -eq 0) { $inGit = $true } } catch {}

if ($inGit) {
  $branch = (git rev-parse --abbrev-ref HEAD).Trim()
  Write-Gray "Git branch: $branch"
  $dirty = git status --porcelain
  if ($dirty) { Write-Yellow "ATTENZIONE: Working tree non pulita. Consigliato commit/stash PRIMA della pulizia." }
}

# --- Utilità ---
function Remove-Target($path) {
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
    Write-Yellow "Skip: $path -> $($_.Exception.Message)"
    return $false
  }
}

$toRemove = New-Object System.Collections.Generic.List[string]

# --- 1) Lettura CSV e raccolta target DELETE ---
if ($DeleteCsv) {
  if (-not (Test-Path $DeleteCsv)) { Write-Red "CSV non trovato: $DeleteCsv"; exit 1 }
  $rows = Import-Csv $DeleteCsv
  foreach ($r in $rows) {
    $suggest = ($r.suggestion, $r.Suggestion | Where-Object { $_ })[0]
    if ($suggest -and $suggest -like "DELETE*") {
      $p = ($r.path, $r.Path | Where-Object { $_ })[0]
      if (-not $p) { continue }
      $p = $p -replace "[/\\]+","/"
      # Candidato 1: diretto
      $c1 = Join-Path (Get-Location).Path ($p -replace "/","\\")
      $matched = $false
      if (Test-Path $c1) { $toRemove.Add($c1); $matched = $true }
      if (-not $matched) {
        # Candidato 2: rimuovi primo segmento
        $parts = $p -split "/"
        if ($parts.Length -gt 1) {
          $p2 = ($parts[1..($parts.Length-1)] -join "/")
          $c2 = Join-Path (Get-Location).Path ($p2 -replace "/","\\")
          if (Test-Path $c2) { $toRemove.Add($c2); $matched = $true }
        }
      }
      if (-not $matched) {
        Write-Gray "Non trovato dal CSV: $p"
      }
    }
  }
}

# --- 2) Heuristic fallback (opzionale) ---
if ($HeuristicFallback -or (-not $DeleteCsv)) {
  $dirPatterns = @(
    "**/bin",
    "**/obj",
    ".vs",
    ".idea",
    ".vscode",
    if (-not $KeepOut) { "out" },
    "out/stage",
    "TestResults",
    "artifacts",
    "coverage*",
    ".cache",
    "_UpgradeBackup",
    "packages"
  )
  # Se il repo usa packages.config, NON toccare 'packages/'
  $hasPackagesConfig = @(Get-ChildItem -Recurse -Filter "packages.config" -ErrorAction SilentlyContinue).Count -gt 0
  if ($hasPackagesConfig) {
    $dirPatterns = $dirPatterns | Where-Object { $_ -ne "packages" }
    Write-Yellow "Rilevato packages.config → non elimino 'packages/'"
  }

  foreach ($p in $dirPatterns) {
    Get-ChildItem -Path $p -Directory -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
      $toRemove.Add($_.FullName)
    }
  }

  $filePatterns = @("*.user","*.suo","*.tmp","*.log","*.db-wal","*.db-shm","Thumbs.db",".DS_Store")
  foreach ($fp in $filePatterns) {
    Get-ChildItem -Path . -Recurse -Include $fp -ErrorAction SilentlyContinue | ForEach-Object {
      $toRemove.Add($_.FullName)
    }
  }
}

# --- De-duplica & Conferma ---
$targets = $toRemove | Sort-Object -Unique

if (-not $NoConfirm) {
  Write-Host "Verranno rimossi $($targets.Count) elementi." -ForegroundColor Cyan
  $answer = Read-Host "Confermi? (Y/N)"
  if ($answer.Trim().ToUpper() -ne "Y") {
    Write-Host "Annullato."
    Pop-Location
    exit 0
  }
}

# --- Esecuzione ---
$removed = 0; $missing = 0
foreach ($t in $targets) {
  if (Test-Path $t) {
    if (Remove-Target $t) { $removed++ } else { Write-Yellow "Non rimosso: $t" }
  } else {
    $missing++
  }
}

# --- Git untrack opzionale ---
if ($GitUntrack -and $inGit) {
  $untrackPatterns = @("**/bin","**/obj",".vs",".idea",".vscode","out","TestResults","artifacts","coverage*",".cache","_UpgradeBackup","packages")
  foreach ($p in $untrackPatterns) {
    try {
      git rm -r --cached $p *> $null
    } catch {}
  }
  if (-not $DryRun) { git add . *> $null }
  Write-Gray "Git index ripulito (se presente)."
}

Write-Host "✅ Rimozione completata. Removed=$removed, Missing=$missing, DryRun=$DryRun" -ForegroundColor Green

Pop-Location
