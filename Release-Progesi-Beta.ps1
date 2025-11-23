param(
  [string]$Version = "v0.9.0-beta",
  [string]$SourceBranch = "s2-c2_pure-sqlite-gh",
  [switch]$RunTests
)

$ErrorActionPreference = 'Stop'

function G { param([string[]]$A) & git @A; if ($LASTEXITCODE -ne 0) { throw ("git " + ($A -join ' ') + " -> " + $LASTEXITCODE) } }

# 0) sync
G @('fetch','--all','--prune') | Out-Null
G @('checkout', $SourceBranch)

# 1) branch di release (se serve)
$releaseBranch = "release/$Version"
& git rev-parse --verify --quiet ("refs/heads/" + $releaseBranch) *> $null
if ($LASTEXITCODE -eq 0) { G @('switch', $releaseBranch) } else { G @('switch','-c', $releaseBranch) }

# 2) build/test opzionali
$sln = Get-ChildItem -Filter *.sln | Select-Object -First 1
if ($RunTests) {
  & dotnet clean $sln.FullName
  & dotnet build $sln.FullName -c Release
  & dotnet test  $sln.FullName -c Release --no-build
}

# 3) pack zip
& "$PSScriptRoot\Make-Releases-Save-Progesi-S2-C2.ps1"
$zip = Join-Path $PSScriptRoot 'progesi-gh.zip'
if (-not (Test-Path $zip)) { throw "Zip non trovato: $zip" }

# 4) commit e push ramo
$porc = (git status --porcelain)
if ($porc) { G @('add','-A'); G @('commit','-m', "Release $Version (beta)") }
try { G @('push') } catch { G @('push','-u','origin', $releaseBranch) }

# 5) tag + push tag
G @('tag','-a', $Version,'-m', "Progesi Versione Beta $Version")
G @('push','origin',$Version)

Write-Host "[OK] Ramo $releaseBranch e tag $Version pubblicati."
Write-Host "Carica lo zip $zip nella release $Version su GitHub (o usa gh release create)."
