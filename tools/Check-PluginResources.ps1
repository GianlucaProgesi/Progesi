[CmdletBinding()] param()
$ErrorActionPreference='Stop'

$gha = Join-Path $env:APPDATA 'Grasshopper\Libraries\Progesi\ProgesiGrasshopperAssembly.gha'
Add-Type -AssemblyName System.Reflection
$asm = [System.Reflection.Assembly]::LoadFile($gha)
$asm.GetManifestResourceNames() | Where-Object { $_ -like '*.Resources.*.png' }
