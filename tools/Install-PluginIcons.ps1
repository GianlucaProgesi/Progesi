<# Install-PluginIcons.ps1
Copia le icone PNG nella cartella usata da ProgesiIcons.cs:
  %APPDATA%\Grasshopper\Libraries\Progesi\icons
USO:
  .\tools\Install-PluginIcons.ps1 -Source ".\src\ProgesiGrasshopperAssembly\Resources"
#>

[CmdletBinding()] param(
  [Parameter(Mandatory=$true)] [string]$Source
)
$ErrorActionPreference='Stop'

$src = (Resolve-Path $Source).Path
if (!(Test-Path $src)) { throw "Cartella sorgente icone non trovata: $Source" }

$dst = Join-Path $env:APPDATA "Grasshopper\Libraries\Progesi\icons"
New-Item -ItemType Directory -Force -Path $dst | Out-Null

$needed = @("metin.png","metout.png","varin.png","varout.png","snip.png")
foreach ($n in $needed) {
  $file = Join-Path $src $n
  if (!(Test-Path $file)) { Write-Warning "Icona mancante: $n (non bloccante)"; continue }
  Copy-Item -Force $file (Join-Path $dst $n)
}

Write-Host "Icone installate in $dst" -ForegroundColor Green
Write-Host "Chiudi e riapri Rhino/GH per vederle nel ribbon."
