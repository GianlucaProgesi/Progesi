<#
.SYNOPSIS
  Quick reference per il flusso di release di Progesi + stato pacchetti su NuGet.org.

.USAGE
  pwsh -File ./tools/Release-QuickRef.ps1
#>

param(
  # Elenco pacchetti da verificare su NuGet
  [string[]]$Packages = @(
    'ProgesiCore',
    'ProgesiRepositories.InMemory',
    'ProgesiRepositories.Rhino',
    'ProgesiRepositories.Sqlite'
  )
)

$ErrorActionPreference = 'Stop'

function Get-LastTag {
  $ErrorActionPreference = 'SilentlyContinue'
  $t = (git tag --list 'v*' --sort=-v:refname | Select-Object -First 1)
  $ErrorActionPreference = 'Stop'
  if (-not $t) { return $null }
  return $t
}

function Use-Tls12 {
  try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
  } catch { }
}

function Get-NuGetLatestStable {
  param([Parameter(Mandatory=$true)][string]$Id)
  # Usa il flat container che è leggero e affidabile:
  # GET https://api.nuget.org/v3-flatcontainer/{id-lower}/index.json
  $lower = $Id.ToLowerInvariant()
  $url = "https://api.nuget.org/v3-flatcontainer/$lower/index.json"
  try {
    Use-Tls12
    $resp = Invoke-RestMethod -Method GET -Uri $url -TimeoutSec 15
    if (-not $resp -or -not $resp.versions) { return $null }
    # prendi l'ultima versione STABILE (niente pre-release con '-')
    $stable = @($resp.versions | Where-Object { $_ -notmatch '-' })
    if ($stable.Count -eq 0) { return $null }
    return $stable[-1]
  } catch {
    return $null
  }
}

function Pad([string]$s, [int]$n) { if ($null -eq $s) { $s = '' }; return ($s + (' ' * [Math]::Max(0,$n - $s.Length))) }

# Header
Write-Host ""
Write-Host "🚀 Progesi Release – Quick Reference" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan

$lastTag = Get-LastTag
if ($lastTag) {
  Write-Host ("Ultima versione rilasciata: {0}" -f $lastTag) -ForegroundColor Magenta
} else {
  Write-Host "Ultima versione rilasciata: (nessun tag trovato)" -ForegroundColor Yellow
}
Write-Host ""

# Quick commands
Write-Host "🔎 Dry run (simulazione):" -ForegroundColor Yellow
Write-Host "   pwsh -File ./tools/End-to-End-Release.ps1 -DryRun" -ForegroundColor Green
Write-Host ""
Write-Host "✅ Release reale:" -ForegroundColor Yellow
Write-Host "   pwsh -File ./tools/End-to-End-Release.ps1" -ForegroundColor Green
Write-Host ""
Write-Host "🧪 Pre-release (es. v1.2.3-beta.1):" -ForegroundColor Yellow
Write-Host "   pwsh -File ./tools/End-to-End-Release.ps1 -Pre 'beta.1'" -ForegroundColor Green
Write-Host ""
Write-Host "📖 Vedi docs/RELEASE-FLOW.md per il flusso completo." -ForegroundColor Cyan
Write-Host ""

# NuGet status
Write-Host "📦 Stato pacchetti su NuGet.org (stable):" -ForegroundColor Cyan
$targetVer = $null
if ($lastTag -and $lastTag -match '^v(\d+\.\d+\.\d+)$') { $targetVer = $Matches[1] }

# Tabellina semplice
$col1 = 34; $col2 = 18; $col3 = 10; $col4 = 25
Write-Host ( (Pad "Package" $col1) + (Pad "NuGet (stable)" $col2) + (Pad "Match" $col3) + "Note" )
Write-Host ( ('-' * ($col1 + $col2 + $col3 + 5 + $col4)) )

foreach ($id in $Packages) {
  $nugetVer = Get-NuGetLatestStable -Id $id
  $match = ''
  $note  = ''
  if ($null -eq $nugetVer) {
    $match = 'n/a'
    $note  = 'non trovato / rete?'
  } else {
    if ($targetVer) {
      if ($nugetVer -eq $targetVer) { $match = 'YES' }
      else { $match = 'NO'; $note = "repo: $targetVer" }
    } else {
      $match = '-'
      $note  = 'nessun tag locale'
    }
  }
  Write-Host ( (Pad $id $col1) + (Pad ($nugetVer ?? '-') $col2) + (Pad $match $col3) + $note )
}

Write-Host ""
