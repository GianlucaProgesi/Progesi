<# Publish-Plugin.ps1
Crea lo zip di release (.gha + dll + runtimes + VERSION/CHANGELOG + docs + demo + icons).
USO:
  .\tools\Publish-Plugin.ps1 -Configuration Release -Out ".\dist"
#>

[CmdletBinding()] param(
  [string]$Configuration = "Release",
  [string]$Out = ".\dist"
)
$ErrorActionPreference='Stop'

# build
dotnet build -c $Configuration | Out-Null

# output
$root = (Get-Location).Path
$bin1 = Join-Path $root "src\ProgesiGrasshopperAssembly\bin\$Configuration\net48"
$bin2 = Join-Path $root "src\ProgesiGrasshopperAssembly\bin\$Configuration"
$src  = if (Test-Path $bin1) { $bin1 } elseif (Test-Path $bin2) { $bin2 } else { throw "Build output non trovato." }

# staging
$ver  = (Get-Content ".\VERSION.txt" -ErrorAction SilentlyContinue | Select-String -Pattern 'Version:\s*(.+)$').Matches.Groups[1].Value
if (-not $ver) { $ver = Get-Date -Format "yyyyMMdd_HHmmss" }
$staging = Join-Path $Out ("ProgesiPlugin_" + $ver)
New-Item -ItemType Directory -Force -Path $staging | Out-Null

# Assicura .gha
if (Test-Path (Join-Path $src 'ProgesiGrasshopperAssembly.dll')) {
  Copy-Item -Force (Join-Path $src 'ProgesiGrasshopperAssembly.dll') (Join-Path $src 'ProgesiGrasshopperAssembly.gha')
}

# Copia gha+dll+runtimes
robocopy $src $staging *.gha *.dll /E /R:1 /W:1 | Out-Null
if (Test-Path (Join-Path $src 'runtimes')) {
  robocopy (Join-Path $src 'runtimes') (Join-Path $staging 'runtimes') *.* /E /R:1 /W:1 | Out-Null
}

# VERSION, CHANGELOG, README se presenti
foreach ($f in @("VERSION.txt","CHANGELOG.md","README.txt")) {
  if (Test-Path $f) { Copy-Item $f $staging -Force }
}

# docs (demo misto)
if (Test-Path ".\docs") { robocopy ".\docs" (Join-Path $staging "docs") *.* /S /R:1 /W:1 | Out-Null }

# demo canvas
if (Test-Path ".\tests\P7\GH") { robocopy ".\tests\P7\GH" (Join-Path $staging "demo") *.gh *.ghx /S /R:1 /W:1 | Out-Null }

# icons (se presenti nel repo)
if (Test-Path ".\src\ProgesiGrasshopperAssembly\Resources") {
  $icons = Join-Path $staging "icons"
  New-Item -ItemType Directory -Force -Path $icons | Out-Null
  robocopy ".\src\ProgesiGrasshopperAssembly\Resources" $icons *.png /R:1 /W:1 | Out-Null
}

# zip
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$zip = Join-Path $Out ("ProgesiPlugin_" + $ver + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
Write-Host "ZIP creato: $zip" -ForegroundColor Green
