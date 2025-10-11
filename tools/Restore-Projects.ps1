<# 
  Restore-Projects.ps1
  Ripristina i progetti chiave e la .sln dallo storico Git SENZA hard reset.
#>

Param(
  [string]$Solution = "Progesi.sln",
  [string[]]$ProjectDirs = @(
    "src/ProgesiDataExchange",
    "src/ProgesiDomainServices",
    "src/ProgesiGrasshopperAssembly",
    "src/ProgesiGrasshopperBrowsers",
    "src/ProgesiRepositories.Rhino",
    "src/ProgesiRepositories.Sqlite"
  )
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# --- sanity git ---
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Fail "Non sei in una repo Git."; exit 1 }

# --- paracadute + branch ---
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$tagName = "backup-pre-recovery-$ts"
$branchName = "recovery/restore-solution-$ts"

Info "Fetch (origin) + tag di backup + branch recovery…"
git fetch origin --prune *> $null  # <-- SOLO origin, niente remoti rotti

# crea il tag se non esiste
$tagExists = (git tag --list $tagName | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($tagExists)) { git tag $tagName *> $null } else { Warn "Tag '$tagName' già esistente (ok)." }

# crea il branch
git switch -c $branchName *> $null
if ($LASTEXITCODE -ne 0) { git checkout -b $branchName *> $null }

# --- helpers ---
function FindLastSha([string]$path) {
  $sha = (git rev-list -n 1 --all -- $path | Out-String).Trim()
  return $sha
}
function GitShowFile([string]$sha,[string]$path) {
  $spec = ("{0}:{1}" -f $sha, $path)
  return (git show $spec | Out-String)
}
function AddProjectToSlnIfMissing([string]$sln,[string]$projDir) {
  if (-not (Test-Path $projDir)) { return }
  $csproj = Get-ChildItem -Path $projDir -Filter *.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $csproj) { return }
  $projName = [System.IO.Path]::GetFileNameWithoutExtension($csproj.Name)
  $slnTxt = ""
  if (Test-Path $sln) { $slnTxt = (Get-Content $sln -Raw) }
  if ($slnTxt -match [regex]::Escape($projName)) { return }
  Info ("Aggiungo alla soluzione: {0}" -f $csproj.FullName)
  dotnet sln $sln add $csproj.FullName *> $null
}

# --- ripristina progetti ---
$report = New-Object System.Collections.Generic.List[string]
$missing = @()

foreach ($dir in $ProjectDirs) {
  $sha = FindLastSha $dir
  if ([string]::IsNullOrWhiteSpace($sha)) { Warn "Nello storico non ho trovato: $dir"; $missing += $dir; continue }
  Info ("Ripristino {0} da {1}…" -f $dir,$sha)
  git checkout $sha -- $dir
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dir)) { Fail "Ripristino fallito per $dir"; exit 1 }
  $report.Add(("RESTORED {0} from {1}" -f $dir,$sha))
}

# --- ripristina .sln (ultimo SHA disponibile) ---
$restoreSlnSha = FindLastSha $Solution
if ([string]::IsNullOrWhiteSpace($restoreSlnSha)) {
  Warn ("Nello storico non ho trovato {0}. Proseguo." -f $Solution)
} else {
  Info ("Ripristino {0} da {1}…" -f $Solution,$restoreSlnSha)
  git checkout $restoreSlnSha -- $Solution
  if ($LASTEXITCODE -ne 0) { Warn "Ripristino .sln fallito; proseguo." } else { $report.Add(("RESTORED {0} from {1}" -f $Solution,$restoreSlnSha)) }
}

# --- assicurati che i .csproj siano inclusi in .sln ---
if (Test-Path $Solution) {
  foreach ($d in $ProjectDirs) { AddProjectToSlnIfMissing -sln $Solution -projDir $d }
} else {
  Warn "La soluzione $Solution non esiste; salto l’aggiunta automatica dei .csproj."
}

# --- commit + push ---
$repFile = "RECOVERY-REPORT-$ts.txt"
$report | Set-Content -Encoding UTF8 $repFile
git add $ProjectDirs $Solution $repFile -A *> $null
git commit -m "recovery: restore projects from history (see $repFile)" *> $null
git push -u origin $branchName

Ok ("Ripristino completato sul branch: {0}" -f $branchName)
Ok ("Report: {0}" -f $repFile)
Write-Host "`nApri PR verso main (Squash). Dopo il merge ricostruisci la solution in VS." -ForegroundColor Cyan
