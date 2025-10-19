[CmdletBinding()]
param(
  [ValidateSet("Release","Debug")] [string]$Configuration = "Release",
  [switch]$Rebuild,     # forza build
  [switch]$CleanBefore  # rimuove versioni precedenti
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# root repo
$repo = (git rev-parse --show-toplevel 2>$null)
if (-not $repo) { $repo = (Get-Location).Path }
$repo = (Resolve-Path $repo).Path

# cartella Libraries utente
$lib  = Join-Path $env:APPDATA "Grasshopper\Libraries"
New-Item -ItemType Directory -Force -Path $lib | Out-Null

# build se richiesto
if ($Rebuild) {
  Info "dotnet restore/build ($Configuration)…"
  dotnet restore "$repo\Progesi.sln" --nologo
  dotnet build   "$repo\Progesi.sln" -c $Configuration --nologo
}

# trova output più recente
function Find-Last([string]$rel){
  $root = Join-Path $repo $rel
  if (-not (Test-Path $root)) { return $null }
  Get-ChildItem -Path $root -Recurse -Filter *.dll |
    Where-Object { $_.Name -match 'ProgesiGrasshopper(Assembly|Browsers)\.dll' } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

$asmDll = Find-Last "src\ProgesiGrasshopperAssembly\bin"
$brwDll = Find-Last "src\ProgesiGrasshopperBrowsers\bin"
if (-not $asmDll -or -not $brwDll) {
  Info "Output non trovato → build ($Configuration)…"
  dotnet restore "$repo\Progesi.sln" --nologo
  dotnet build   "$repo\Progesi.sln" -c $Configuration --nologo
  $asmDll = Find-Last "src\ProgesiGrasshopperAssembly\bin"
  $brwDll = Find-Last "src\ProgesiGrasshopperBrowsers\bin"
}
if (-not $asmDll) { Fail "DLL Assembly non trovata"; exit 1 }
if (-not $brwDll) { Fail "DLL Browsers non trovata"; exit 1 }

$asmDir = $asmDll.Directory.FullName
$brwDir = $brwDll.Directory.FullName
Info "Assembly out:  $asmDir"
Info "Browsers out:  $brwDir"

# pulizia
if ($CleanBefore) {
  Get-ChildItem $lib -Filter "ProgesiGrasshopper*.gha" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
  Get-ChildItem $lib -Filter "*.dll"                      -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 1) copia **tutte** le dll dall’output Assembly (include ClosedXML, DocumentFormat.OpenXml, ecc.)
Get-ChildItem $asmDir -Filter *.dll | ForEach-Object {
  Copy-Item $_.FullName (Join-Path $lib $_.Name) -Force
}
# 2) copia anche eventuali dll extra dai Browsers
Get-ChildItem $brwDir -Filter *.dll | ForEach-Object {
  Copy-Item $_.FullName (Join-Path $lib $_.Name) -Force
}

# 3) copia la **native** e_sqlite3.dll (necessaria per Microsoft.Data.Sqlite)
$e = Get-ChildItem "$env:USERPROFILE\.nuget\packages\sqlitepclraw.bundle_e_sqlite3\2.1.*\runtimes\win-x64\native\e_sqlite3.dll" -ErrorAction SilentlyContinue |
     Sort-Object FullName -Descending | Select-Object -First 1
if ($e) {
  Copy-Item $e.FullName (Join-Path $lib "e_sqlite3.dll") -Force
  Ok "Copiata native: e_sqlite3.dll"
} else {
  Warn "ATTENZIONE: non trovata e_sqlite3.dll. Installa pacchetto SQLitePCLRaw.bundle_e_sqlite3 (2.1.x) oppure copia a mano."
}

# 4) duplica le due plugin come .gha
Copy-Item $asmDll.FullName (Join-Path $lib "ProgesiGrasshopperAssembly.gha") -Force
Copy-Item $brwDll.FullName (Join-Path $lib "ProgesiGrasshopperBrowsers.gha") -Force

# 5) risorse icone
$icons = Join-Path $repo "src\ProgesiGrasshopperAssembly\Resources"
if (Test-Path $icons) {
  New-Item -ItemType Directory -Force -Path (Join-Path $lib "Resources") | Out-Null
  Copy-Item "$icons\*.png" (Join-Path $lib "Resources") -Force -ErrorAction SilentlyContinue
}

# 6) Unblock
Get-ChildItem $lib\*.gha | Unblock-File 2>$null
Get-ChildItem $lib\*.dll | Unblock-File 2>$null

Ok "Installazione completata in: $lib"
Write-Host "Chiudi e riapri Rhino/Grasshopper (completamente), poi riprova i test DX."
