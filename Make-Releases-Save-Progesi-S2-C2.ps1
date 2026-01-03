param(
  [string]$Config   = 'Release',
  [string]$Tf       = 'net48',
  [string]$StageDirName = 'Progesi-GH',
  [string]$ZipName  = 'progesi-gh.zip'
)

$ErrorActionPreference = 'Stop'

# Paths
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$BinDir   = Join-Path $RepoRoot "src\ProgesiGrasshopperAssembly\bin\$Config\$Tf"
$StageDir = Join-Path $RepoRoot "artifacts\$StageDirName"
$ZipPath  = Join-Path $RepoRoot $ZipName

if (-not (Test-Path $BinDir)) { throw "Output non trovato: $BinDir. Esegui prima 'dotnet build -c $Config'." }

# Pulisci staging
if (Test-Path $StageDir) { Remove-Item $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir | Out-Null

# Copia selettiva: solo file ammessi
$include = @(
  'Progesi*.gha',
  'Progesi*.dll',
  'System.Data.SQLite.dll',
  'SQLite.Interop.dll',
  'x64\SQLite.Interop.dll',
  'x86\SQLite.Interop.dll',
  'Microsoft.Data.Sqlite*.dll',
  'System.*.dll',
  'netstandard.dll',
  'Newtonsoft.Json*.dll',
  'ClosedXML*.dll',
  'DocumentFormat.OpenXml*.dll'
)

Get-ChildItem -Path $BinDir -Recurse -File | ForEach-Object {
  $name = $_.Name
  if ($include | Where-Object { $name -like $_ }) {
    $dest = Join-Path $StageDir ($_.FullName.Substring($BinDir.Length).TrimStart('\','/'))
    $destDir = Split-Path $dest -Parent
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir | Out-Null }
    Copy-Item $_.FullName $dest -Force
  }
}

# Icone content (se presenti)
$icons = Join-Path $RepoRoot 'src\ProgesiGrasshopperAssembly\Resources\Icons'
$destIcons = Join-Path $StageDir 'Resources\Icons'
if (Test-Path $icons) {
  if (-not (Test-Path $destIcons)) { New-Item -ItemType Directory -Path $destIcons | Out-Null }
  Copy-Item -Path (Join-Path $icons '*') -Destination $destIcons -Recurse -Force
} else {
  Write-Host "[INFO] Nessuna cartella icone trovata in $icons (skip)."
}

# Documentazione (se presente)
$Readme = Join-Path $RepoRoot 'README.md'
$Docs   = Join-Path $RepoRoot 'docs'
if (Test-Path $Readme) { Copy-Item $Readme (Join-Path $StageDir 'README.md') -Force }
if (Test-Path $Docs)   { Copy-Item $Docs   (Join-Path $StageDir 'docs') -Recurse -Force }

# Zip (sovrascrive se esiste)
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $ZipPath

Write-Host "[OK] Package creato:" $ZipPath
