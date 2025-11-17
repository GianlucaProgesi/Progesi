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

if (-not (Test-Path $BinDir)) {
  throw "Output non trovato: $BinDir. Esegui prima 'dotnet build -c $Config'."
}

# Pulisci staging
if (Test-Path $StageDir) {
  Remove-Item $StageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StageDir | Out-Null

# Copia i binari del plugin GH (escludi pdb/xml/log/exe)
$exclude = @('*.pdb','*.xml','*.tmp','*.log','*.exe')
Copy-Item -Path (Join-Path $BinDir '*') -Destination $StageDir -Recurse -Force -Exclude $exclude

# Aggiungi documentazione se presente
$Readme = Join-Path $RepoRoot 'README.md'
$Docs   = Join-Path $RepoRoot 'docs'
if (Test-Path $Readme) { Copy-Item $Readme (Join-Path $StageDir 'README.md') -Force }
if (Test-Path $Docs)   { Copy-Item $Docs   (Join-Path $StageDir 'docs') -Recurse -Force }

# Crea lo zip (sovrascrive se esiste)
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $StageDir '*') -DestinationPath $ZipPath

Write-Host "[OK] Package creato:" $ZipPath
