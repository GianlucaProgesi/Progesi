[CmdletBinding()]
param(
  [string]$NupkgDir = './nupkg',
  [switch]$FailOnWarning = $false
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Test-ReadmeInNupkg([string]$nupkg) {
  $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
  try {
    $has = $false
    foreach ($e in $zip.Entries) {
      if ($e.FullName -match '^(README|readme).md$') { $has = $true; break }
    }
    return $has
  } finally { $zip.Dispose() }
}

$errors = 0; $warns = 0
$nupkgs = Get-ChildItem -Path $NupkgDir -Filter *.nupkg
if (-not $nupkgs) { Write-Error "Nessun .nupkg in $NupkgDir"; exit 1 }

foreach ($pkg in $nupkgs) {
  $base = [System.IO.Path]::GetFileNameWithoutExtension($pkg.Name)
  $snup = Join-Path $NupkgDir ($base + '.snupkg')

  if (-not (Test-ReadmeInNupkg $pkg.FullName)) {
    Write-Host "ERR: manca README.md in $($pkg.Name)" -ForegroundColor Red
    $errors++
  }

  if (-not (Test-Path $snup)) {
    Write-Host "ERR: manca snupkg per $($pkg.Name)" -ForegroundColor Red
    $errors++
  }

  # SourceLink test (best-effort)
  $hasTool = (dotnet tool list -g | Select-String -Quiet 'dotnet-sourcelink')
  if ($hasTool) {
    $tmp = New-Item -ItemType Directory -Path (Join-Path $env:RUNNER_TEMP "unzip_$base") -Force
    [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg.FullName, $tmp.FullName, $true)
    $dlls = Get-ChildItem -Path $tmp.FullName -Recurse -Filter *.dll
    foreach ($d in $dlls) {
      $res = & dotnet sourcelink test $d.FullName 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Host "WARN: sourcelink fallito per $($d.Name): $res" -ForegroundColor Yellow
        $warns++
      }
    }
  }
}

if ($errors -gt 0) { Write-Error "Quality gate FAILED: $errors errori, $warns warning"; exit 1 }
if ($FailOnWarning -and $warns -gt 0) { Write-Error "Quality gate FAILED per warning ($warns)"; exit 2 }

Write-Host "Quality gate OK: $($nupkgs.Count) pacchetti, $errors errori, $warns warning" -ForegroundColor Green
