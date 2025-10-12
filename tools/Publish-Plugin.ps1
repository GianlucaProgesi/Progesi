[CmdletBinding()]
param(
  [ValidateSet("Debug","Release")]
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Repo root = .. dal folder tools
$repo = Resolve-Path (Join-Path $PSScriptRoot "..") | Select-Object -ExpandProperty Path
$outRoot = Join-Path $repo "out"
$stage   = Join-Path $outRoot "plugin_pack"

if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

# Output principale del componente GH (net48)
$asmOut = Join-Path $repo "src\ProgesiGrasshopperAssembly\bin\$Configuration\net48"
if (-not (Test-Path $asmOut)) {
  throw "Output non trovato: $asmOut. Hai compilato in $Configuration?"
}

# Copia TUTTO l’output del componente (dll dipendenze incluse)
Copy-Item "$asmOut\*.*" $stage -Recurse -Force

# Crea anche la .gha (copia della dll principale)
$dll = Join-Path $stage "ProgesiGrasshopperAssembly.dll"
if (Test-Path $dll) {
  Copy-Item $dll (Join-Path $stage "ProgesiGrasshopperAssembly.gha") -Force
}

# Risorse (icone)
$iconsDir = Join-Path $repo "src\ProgesiGrasshopperAssembly\Resources"
if (Test-Path $iconsDir) {
  New-Item -ItemType Directory -Force -Path (Join-Path $stage "Resources") | Out-Null
  Copy-Item "$iconsDir\*.png" (Join-Path $stage "Resources") -Force -ErrorAction SilentlyContinue
}

# README minimale
$readme = Join-Path $stage "README.txt"
"Progesi Grasshopper Tools - package $Configuration`r`nContiene .gha, dll e risorse." | Set-Content -Encoding UTF8 $readme

# ZIP
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zip = Join-Path $outRoot ("ProgesiPlugin_{0}.zip" -f $stamp)
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip

Write-Host "ZIP creato: $zip"
