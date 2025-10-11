Param(
  [string]$Solution = "Progesi.sln"
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# Repo root sanity
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Fail "Non sei dentro una repo Git."; exit 1 }

if (-not (Test-Path $Solution)) { Fail "Non trovo $Solution nella cartella corrente."; exit 1 }

# 1) Leggi i progetti inclusi nella sln e rimuovi quelli che non esistono su disco
Info "Analisi solution per riferimenti rotti..."
$included = @()
$rawList = (dotnet sln $Solution list | Out-String)
$lines = $rawList -split "`r?`n"
foreach ($ln in $lines) {
  if ($ln -match '\.csproj\s*$') {
    $p = $ln.Trim()
    # normalizza separatori
    $p = $p -replace '/', '\'
    $included += $p
  }
}

$removed = @()
foreach ($proj in $included) {
  if (-not (Test-Path $proj)) {
    Warn "Riferimento rotto: $proj -> RIMUOVO dalla solution"
    dotnet sln $Solution remove $proj *> $null
    $removed += $proj
  }
}

# 2) Scansiona i .csproj reali in src/ e tests/ e aggiungili se mancanti
Info "Ricerca .csproj su disco (src/, tests/)..."
$physical = Get-ChildItem -Path src, tests -Recurse -Filter *.csproj -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }

# set di inclusi aggiornato dopo eventuali remove
$rawList2 = (dotnet sln $Solution list | Out-String)
$included2 = @()
foreach ($ln in ($rawList2 -split "`r?`n")) {
  if ($ln -match '\.csproj\s*$') {
    $p = $ln.Trim() -replace '/', '\'
    $included2 += (Resolve-Path $p).Path
  }
}

$added = @()
foreach ($full in $physical) {
  $norm = (Resolve-Path $full).Path
  if (-not ($included2 -contains $norm)) {
    Info "Aggiungo alla solution: $norm"
    dotnet sln $Solution add $norm *> $null
    $added += $norm
  }
}

# 3) Rimuovi SourceGear.sqlite3 ovunque (rompe Any CPU)
Info "Rimozione SourceGear.sqlite3 (se presente)..."
$allCsproj = Get-ChildItem -Path . -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
foreach ($f in $allCsproj) {
  try { dotnet remove $f.FullName package SourceGear.sqlite3 *> $null } catch { }
}

# 4) Allinea SQLitePCLRaw.bundle_e_sqlite3 = 3.0.2 in Sqlite lib e tests
Info "Allineamento SQLitePCLRaw.bundle_e_sqlite3=3.0.2..."
# Libreria Sqlite
$libSqlite = Get-ChildItem -Path src -Recurse -Filter *ProgesiRepositories.Sqlite*.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
if ($libSqlite) {
  dotnet add $libSqlite.FullName package SQLitePCLRaw.bundle_e_sqlite3 --version 3.0.2 *> $null
}

# Test Sqlite
$testSqlite = Get-ChildItem -Path tests -Recurse -Filter *ProgesiRepositories.Sqlite*.Tests*.csproj -ErrorAction SilentlyContinue | Select-Object -First 1
if ($testSqlite) {
  dotnet add $testSqlite.FullName package SQLitePCLRaw.bundle_e_sqlite3 --version 3.0.2 *> $null
}

# 5) Restore / Build / Test (senza errori di sintassi)
Info "dotnet restore..."
dotnet restore $Solution --nologo

Info "dotnet build (Release)..."
dotnet build $Solution -c Release --nologo --no-restore

Info "dotnet test (Release, no-build)..."
dotnet test $Solution -c Release --no-build --nologo

Ok "Operazione completata."
if ($removed.Count -gt 0) { Write-Host "Rimossi dalla sln:" -ForegroundColor Yellow; $removed | ForEach-Object { "  - $_" | Write-Host } }
if ($added.Count -gt 0)   { Write-Host "Aggiunti alla sln:" -ForegroundColor Yellow; $added   | ForEach-Object { "  + $_" | Write-Host } }
