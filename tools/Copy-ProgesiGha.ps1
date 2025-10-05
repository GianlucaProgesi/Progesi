# tools/Copy-ProgesiGha.ps1
# Copia ProgesiGrasshopperAssembly.gha e tutte le dipendenze nella cartella Libraries di Grasshopper.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# 1) Chiudi Rhino se aperto (silenzioso)
Get-Process Rhino -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# 2) Percorsi
$tools = Split-Path -Parent $PSCommandPath          # ...\Progesi\tools
$repo  = Split-Path -Parent $tools                   # ...\Progesi
$relBin1 = Join-Path $repo 'src\ProgesiGrasshopperAssembly\bin\Release\net48'
$relBin2 = Join-Path $repo 'src\ProgesiGrasshopperAssembly\bin\Release'

# scegli il bin esistente più "ricco"
$searchRoots = @()
if (Test-Path $relBin1) { $searchRoots += $relBin1 }
if (Test-Path $relBin2) { $searchRoots += $relBin2 }
if ($searchRoots.Count -eq 0) {
    throw "Cartelle build non trovate: $relBin1 o $relBin2. Esegui prima: dotnet build -c Release"
}

# 3) trova l'artefatto più recente (gha o dll) nei root individuati
$gha = Get-ChildItem -Path $searchRoots -Recurse -Filter 'ProgesiGrasshopperAssembly.gha' -EA SilentlyContinue |
       Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$dll = Get-ChildItem -Path $searchRoots -Recurse -Filter 'ProgesiGrasshopperAssembly.dll' -EA SilentlyContinue |
       Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1

$srcDir = $null
if ($gha) { $srcDir = $gha.Directory.FullName }
elseif ($dll) {
    $srcDir = $dll.Directory.FullName
    # assicurati che esista anche la .gha: GH preferisce questa estensione
    $dllPath = Join-Path $srcDir 'ProgesiGrasshopperAssembly.dll'
    $ghaPath = Join-Path $srcDir 'ProgesiGrasshopperAssembly.gha'
    if (Test-Path $dllPath) { Copy-Item -Force $dllPath $ghaPath }
}
else {
    throw "Nessun output trovato sotto: $($searchRoots -join ';')"
}

# 4) destinazione GH
$dst = Join-Path $env:APPDATA 'Grasshopper\Libraries\Progesi'
New-Item -ItemType Directory -Force -Path $dst | Out-Null

# 5) copia gha + tutte le dll e la cartella runtimes (per e_sqlite3)
Write-Host "Copia .GHA e dipendenze:" -ForegroundColor Cyan
Write-Host "  FROM: $srcDir" -ForegroundColor DarkGray
Write-Host "  TO  : $dst"    -ForegroundColor DarkGray

robocopy $srcDir $dst *.gha *.dll /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null

$srcRuntimes = Join-Path $srcDir 'runtimes'
if (Test-Path $srcRuntimes) {
    robocopy $srcRuntimes (Join-Path $dst 'runtimes') *.* /E /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
}

Write-Host "OK. Riavvia Rhino dalla STESSA shell per ereditare le variabili d'ambiente." -ForegroundColor Green
