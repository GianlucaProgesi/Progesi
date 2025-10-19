[CmdletBinding()]
param(
  [ValidateSet("Release","Debug")] [string]$Configuration = "Release",
  [switch]$CleanBefore
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }

# Root repo
$repo = (git rev-parse --show-toplevel 2>$null); if (-not $repo) { $repo = (Get-Location).Path }
$repo = (Resolve-Path $repo).Path

# 1) build
Info "dotnet restore/build ($Configuration)…"
dotnet restore "$repo\Progesi.sln" --nologo
dotnet build   "$repo\Progesi.sln" -c $Configuration --nologo

# 2) cartelle output e Libraries
$asmDir = Join-Path $repo "src\ProgesiGrasshopperAssembly\bin\$Configuration\net48"
$brwDir = Join-Path $repo "src\ProgesiGrasshopperBrowsers\bin\$Configuration\net48"
$lib    = Join-Path $env:APPDATA "Grasshopper\Libraries"
New-Item -ItemType Directory -Force -Path $lib | Out-Null

if ($CleanBefore) {
  Get-ChildItem $lib -Filter "ProgesiGrasshopper*.gha" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 3) installa plugin come .gha
Copy-Item "$asmDir\ProgesiGrasshopperAssembly.dll" "$lib\ProgesiGrasshopperAssembly.gha" -Force
Copy-Item "$brwDir\ProgesiGrasshopperBrowsers.dll" "$lib\ProgesiGrasshopperBrowsers.gha" -Force

# 4) dipendenze (ClosedXML, Sqlite, …)
Get-ChildItem $asmDir -Filter *.dll | Copy-Item -Destination $lib -Force
Get-ChildItem $brwDir -Filter *.dll | Copy-Item -Destination $lib -Force

# 5) native e_sqlite3.dll
$nuget = Join-Path $env:USERPROFILE ".nuget\packages"
$e = Get-ChildItem $nuget -Recurse -Filter e_sqlite3.dll -ErrorAction SilentlyContinue `
     | Where-Object { $_.FullName -match '\\runtimes\\win-x64\\' } `
     | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($e) { Copy-Item $e.FullName (Join-Path $lib "e_sqlite3.dll") -Force }
else    { Warn "e_sqlite3.dll non trovata in cache; se serve:
  dotnet add src\ProgesiRepositories.Sqlite\ProgesiRepositories.Sqlite.csproj package SQLitePCLRaw.bundle_e_sqlite3 --version 2.1.10
  dotnet restore" }

# 6) sblocca
Get-ChildItem $lib\*.gha,$lib\*.dll | Unblock-File 2>$null

Ok "Installato in: $lib"
Write-Host "CHIUDI e RIAPRI Rhino/Grasshopper, poi rifai: Read → Write DB → Write XLSX."
