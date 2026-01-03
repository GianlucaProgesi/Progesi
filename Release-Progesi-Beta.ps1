param(
  [string]$Version      = "v0.9.0-beta",
  [string]$SourceBranch = "s2-c2_pure-sqlite-gh",
  [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
function G { param([string[]]$A) & git @A; if ($LASTEXITCODE -ne 0) { throw ("git " + ($A -join ' ') + " -> " + $LASTEXITCODE) } }

# 0) sync
G @('fetch','--all','--prune') | Out-Null

# 1) assicura working tree pulito (niente switch con file modificati)
$porc = (git status --porcelain)
if ($porc) {
  Write-Warning "Hai modifiche locali non committate. Commit o stash prima della release."
  git status
  break
}

# 2) switch sul branch sorgente e crea/switch sul branch di release
G @('checkout', $SourceBranch)
$rel = "release/$Version"
& git rev-parse --verify --quiet ("refs/heads/" + $rel) *> $null
if ($LASTEXITCODE -eq 0) { G @('switch', $rel) } else { G @('switch','-c', $rel) }

# 3) build/test (opzionali)
$sln = Get-ChildItem -Filter *.sln | Select-Object -First 1
if ($RunTests) {
  & dotnet clean $sln.FullName
  & dotnet build $sln.FullName -c Release
  & dotnet test  $sln.FullName -c Release --no-build
}

# 4) crea zip
& "$PSScriptRoot\Make-Releases-Save-Progesi-S2-C2.ps1"
$zip = Join-Path $PSScriptRoot 'progesi-gh.zip'
if (-not (Test-Path $zip)) { throw "Zip non trovato: $zip" }

# 5) commit branch release (se qualcosa e' cambiato)
$porc2 = (git status --porcelain)
if ($porc2) {
  G @('add','-A')
  G @('commit','-m', "Release $Version (beta)")
}

# 6) push branch release
try { G @('push') } catch { G @('push','-u','origin',$rel) }

# 7) tag (solo se non esiste gia')
$tags = (& git tag) -split "`r?`n"
if ($tags -contains $Version) {
  Write-Host "[INFO] Tag $Version gia' esistente: salto creazione tag."
} else {
  G @('tag','-a', $Version, '-m', "Progesi Versione Beta $Version")
  G @('push','origin', $Version)
}

Write-Host "`n[OK] Branch $rel e tag $Version pubblicati."
Write-Host "Ora puoi aggiornare/creare la release GitHub e allegare lo zip: $zip"
