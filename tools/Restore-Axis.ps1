<# 
  Restore-Axis.ps1
  Ripristina TUTTI i file Axis (Core + Tests) dalla storia Git, senza hard reset:
  - Crea branch di recovery e tag paracadute
  - Individua dai log Git i percorsi storici che contengono "Axis"
  - Ripristina i file al working tree dal loro ultimo commit
  - Aggiunge i .csproj di test in solution, se mancanti
  - Esegue restore/build/test e stampa riepilogo
#>

Param(
  [string]$Solution = "Progesi.sln",
  [string]$AxisFilter = "Axis"  # filtra per 'Axis' / 'ProgesiAxisVariable' etc.
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# --- sanity git ---
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Fail "Non sei in una repo Git."; exit 1 }

# --- branch di recovery + tag paracadute ---
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$recBranch = "recovery/restore-axis-$ts"
$backupTag = "backup-pre-restore-axis-$ts"

Info "Fetch origin --prune..."
git fetch origin --prune *> $null

# tag se non esiste
$exists = (git tag --list $backupTag | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($exists)) { git tag $backupTag *> $null } else { Warn "Tag '$backupTag' già presente." }

# branch nuovo
git checkout -b $recBranch *> $null

# --- trova percorsi storici che contengono 'Axis' (Core + Tests) ---
Info "Analisi storico per individuare file Axis (Core + Tests)..."
$log = git log --all --name-only --pretty=format: | Out-String
$paths = $log -split "`r?`n" | Where-Object {
  $_ -and $_.Trim() -match '\.cs$' -and
  ( $_ -match '^src/ProgesiCore/' -or $_ -match '^tests/' ) -and
  $_ -match $AxisFilter
} | Sort-Object -Unique

if (-not $paths -or $paths.Count -eq 0) {
  Fail "Nello storico non ho trovato file Axis in src/ProgesiCore/ o tests/. Interrompo per evitare danni."
  Write-Host "Se hai uno ZIP di riferimento, espandi i file Axis e poi rilancia." -ForegroundColor Yellow
  exit 1
}

Write-Host "File Axis rilevati nello storico:" -ForegroundColor Yellow
$paths | ForEach-Object { "  - $_" | Write-Host }

# --- ripristina ciascun file dal suo ultimo SHA ---
$restored = New-Object System.Collections.Generic.List[string]
$missed   = New-Object System.Collections.Generic.List[string]

foreach ($p in $paths) {
  $sha = (git rev-list -n 1 --all -- $p | Out-String).Trim()
  if ([string]::IsNullOrWhiteSpace($sha)) { $missed.Add($p); continue }
  Info ("Ripristino {0} da {1}" -f $p,$sha)
  git checkout $sha -- $p
  if ($LASTEXITCODE -eq 0 -and (Test-Path $p)) { $restored.Add($p) } else { $missed.Add($p) }
}

if ($restored.Count -eq 0) {
  Fail "Nessun file ripristinato. Interrompo."
  exit 1
}

# --- assicurati che i progetti di test Axis siano in solution (SDK-style: basta .csproj presente) ---
if (Test-Path $Solution) {
  $slnList = (dotnet sln $Solution list | Out-String)
  # cerca csproj nei tests che contengono "Axis" nel nome/percorsi
  $axisTestCsproj = Get-ChildItem -Path tests -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Axis' }

  foreach ($t in $axisTestCsproj) {
    $name = [IO.Path]::GetFileNameWithoutExtension($t.Name)
    if ($slnList -notmatch [regex]::Escape($name)) {
      Info ("Aggiungo test project in solution: {0}" -f $t.FullName)
      dotnet sln $Solution add $t.FullName *> $null
    }
  }
} else {
  Warn "Solution non trovata ($Solution). Salto l'aggancio automatico dei test."
}

# --- restore / build / test ---
Info "dotnet restore..."
dotnet restore $Solution --nologo

Info "dotnet build (Release)..."
dotnet build $Solution -c Release --nologo --no-restore

Info "dotnet test (Release, no-build)..."
dotnet test $Solution -c Release --no-build --nologo

# --- commit + push ---
$rep = "AXIS-RECOVERY-REPORT-$ts.txt"
"Restored files:" | Set-Content $rep -Encoding UTF8
$restored | Add-Content $rep
if ($missed.Count -gt 0) {
  Add-Content $rep "`nMissed files (no SHA found):"
  $missed | Add-Content $rep
}
git add -A
git commit -m "recovery(axis): restore Axis code & tests from history (see $rep)"
git push -u origin $recBranch

Ok "Ripristino completato sul branch: $recBranch"
Ok "Report: $rep"
Write-Host "Apri PR → main (Squash). Dopo il merge, i test Axis dovrebbero tornare nel conteggio." -ForegroundColor Cyan
