<# 
 Esegue i test dei due progetti + genera Cobertura (MSBuild coverlet e XPlat) + merge + gate.
 Uso:
   pwsh ./tools/coverage.ps1 -RunTests
   pwsh ./tools/coverage.ps1 -MinLine 79
#>

[CmdletBinding()]
param(
  [switch]$RunTests,
  [double]$MinLine
)

$ErrorActionPreference = 'Stop'
$PSStyle.OutputRendering = "PlainText"

function _root() {
  # cartella repo (una su rispetto a /tools)
  return (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

function _projDir([string]$projPath) {
  return (Split-Path -Parent $projPath)
}

function Invoke-Tests([string]$proj) {
  $proj = (Resolve-Path $proj).Path
  $dir  = _projDir $proj

  Write-Host ">> dotnet test ($proj) con coverlet (Cobertura + XPlat)..."

  $args = @(
    'test', $proj, '-c','Debug',
    '/p:CollectCoverage=true',
    '/p:CoverletOutput=TestResults/Coverage/',
    '/p:CoverletOutputFormat=cobertura',
    '--collect:XPlat Code Coverage'         # collector aggiuntivo, nel dubbio
  )

  & dotnet @args

  # 1) Percorso “MSBuild coverlet”
  $covMsbuild = Join-Path $dir 'TestResults/Coverage/coverage.cobertura.xml'

  # 2) Percorso “XPlat collector”: TestResults/<guid>/coverage.cobertura.xml
  $testRes = Join-Path $dir 'TestResults'
  $covXplat = Get-ChildItem -Path $testRes -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue `
             | Sort-Object LastWriteTime -Descending | Select-Object -First 1

  $paths = @()
  if (Test-Path $covMsbuild) { $paths += $covMsbuild }
  if ($covXplat)              { $paths += $covXplat.FullName }

  if ($paths.Count -eq 0) {
    Write-Host "Contenuto TestResults per debug:"
    if (Test-Path $testRes) { Get-ChildItem -Recurse -Path $testRes | ForEach-Object { $_.FullName } | Out-Host }
    throw "Cobertura non trovati per $proj."
  }

  # Ritorno il primo valido (priorità MSBuild)
  return $paths[0]
}

function Merge-And-Print([string[]]$reports) {
  $root = _root
  $outDir = Join-Path $root 'TestResults/MergedCoverage'
  if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
  New-Item -ItemType Directory -Force -Path $outDir | Out-Null

  $joined = ($reports -join ';')

  reportgenerator `
    -reports:$joined `
    -targetdir:$outDir `
    -reporttypes:'Cobertura;TextSummary' | Out-Host

  $cob = Join-Path $outDir 'Cobertura.xml'
  if (!(Test-Path $cob)) { throw "Report merge non creato ($cob)." }

  Write-Host ""
  Write-Host "### Test coverage"
  Write-Host "Report: $cob"
  Write-Host ""
  Get-Content (Join-Path $outDir 'Summary.txt') | Out-Host
}

function Gate([double]$threshold) {
  $root = _root
  $sum  = Join-Path $root 'TestResults/MergedCoverage/Summary.txt'
  if (!(Test-Path $sum)) { throw "Summary non trovato: $sum" }

  $txt = Get-Content $sum -Raw
  $m = [regex]::Match($txt, 'Line coverage:\s*([0-9]+(?:\.[0-9]+)?)%')
  if (!$m.Success) { throw "Impossibile leggere la percentuale di line coverage." }

  $pct = [double]::Parse($m.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)

  Write-Host ""
  Write-Host ("Gate {0}% vs soglia {1}%." -f $pct, $threshold)

  if ($pct -lt $threshold) {
    throw ("Gate FALLITO: coverage {0}% < soglia {1}%." -f $pct, $threshold)
  } else {
    Write-Host ("Gate OK: coverage {0}% >= soglia {1}%." -f $pct, $threshold)
  }
}

# === FLOW ===

$root = _root
$coreProj   = Join-Path $root 'tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj'
$sqliteProj = Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/ProgesiRepositories.Sqlite.Tests.csproj'

if ($RunTests) {
  $cov1 = Invoke-Tests $coreProj
  $cov2 = Invoke-Tests $sqliteProj

  # In casi rari MSBuild può creare il file con un nome tipo coverage.cobertura.xml senza sovrascrivere:
  # se esiste anche XPlat, prendiamo il più recente fra i due per ciascun progetto.
  $reports = @($cov1, $cov2)
  Merge-And-Print $reports
}

if ($PSBoundParameters.ContainsKey('MinLine')) {
  Gate $MinLine
}
