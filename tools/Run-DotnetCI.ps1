<#
.SYNOPSIS
  Restore, build, test (+coverage) e genera report HTML in modo robusto.
  Fallisce se i test falliscono. Se manca il coverage, avvisa ma non fallisce.

.EXAMPLE
  pwsh -File tools/Run-DotnetCI.ps1 -Configuration Release -FallbackCoverlet
#>

[CmdletBinding()]
param(
  # Percorso .sln/.slnf (opzionale). Se vuoto, prova ad auto-scoprirlo.
  [string]$Solution = "",
  [ValidateSet("Debug","Release")]
  [string]$Configuration = "Release",
  # Cartelle di output
  [string]$ResultsDir = "TestResults",
  [string]$CoverageDir = "CoverageReport",
  # Prova un secondo pass con Coverlet MSBuild se XPlat non produce Cobertura
  [switch]$FallbackCoverlet
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet([string[]]$Args) {
  Write-Host ">> dotnet $($Args -join ' ')"
  & dotnet @Args
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet $($Args -join ' ') failed with exit code $LASTEXITCODE"
  }
}

function Ensure-Dir([string]$Path) {
  if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
}

# 1) Info ambiente
Write-Host "=== dotnet --info ==="
Invoke-DotNet @('--info')

# 2) Trova solution se non specificata
if (-not $Solution) {
  $slnf = Get-ChildItem -Recurse -Filter *.slnf -File | Select-Object -First 1
  $sln  = Get-ChildItem -Recurse -Filter *.sln  -File | Select-Object -First 1
  if ($slnf) { $Solution = $slnf.FullName }
  elseif ($sln) { $Solution = $sln.FullName }
  else { $Solution = "" }
}

# 3) Restore + Build
if ($Solution) {
  Invoke-DotNet @('restore', $Solution)
  Invoke-DotNet @('build', $Solution, '--no-restore', '-c', $Configuration)
} else {
  Invoke-DotNet @('restore')
  Invoke-DotNet @('build', '--no-restore', '-c', $Configuration)
}

# 4) Test con XPlat Code Coverage -> Cobertura
Ensure-Dir $ResultsDir
$testArgs = @(
  'test',
  '--no-build', '-c', $Configuration,
  '--results-directory', $ResultsDir,
  '--logger', 'trx;LogFileName=tests.trx',
  '--collect:"XPlat Code Coverage"'
)
# Argomenti per il collector dopo '--'
$collectorArgs = @(
  '--', 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura'
)

if ($Solution) {
  Invoke-DotNet @($testArgs + @($Solution) + $collectorArgs)
} else {
  Invoke-DotNet @($testArgs + $collectorArgs)
}

# 5) Elenco file risultati (debug)
Write-Host "=== Looking for TRX and coverage files ==="
Get-ChildItem -Path $ResultsDir -Recurse -File -Include *.trx,coverage*.xml `
  -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_.FullName }

# 6) Cerca i coverage Cobertura
$coveragePaths = Get-ChildItem -Path $ResultsDir -Recurse -Filter 'coverage.cobertura.xml' `
  -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName

if (-not $coveragePaths) {
  Write-Warning "Nessun 'coverage.cobertura.xml' prodotto dal collector XPlat."
  if ($FallbackCoverlet) {
    Write-Host "Provo il fallback Coverlet MSBuild..."
    $coverletOut = Join-Path $ResultsDir 'coverage'
    Ensure-Dir $coverletOut
    $msbuildArgs = @(
      'test', '--no-build', '-c', $Configuration,
      '/p:CollectCoverage=true',
      '/p:CoverletOutputFormat=cobertura',
      "/p:CoverletOutput=$coverletOut/"
    )
    if ($Solution) { Invoke-DotNet @($msbuildArgs + @($Solution)) }
    else          { Invoke-DotNet @($msbuildArgs) }

    $coveragePaths = Get-ChildItem -Path $coverletOut -Recurse -Filter 'coverage.cobertura.xml' `
      -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
  }
}

# 7) Installa/aggiorna ReportGenerator e prepara PATH
Write-Host "Install/Update ReportGenerator..."
Invoke-DotNet @('tool', 'update', '-g', 'dotnet-reportgenerator-globaltool')

$toolPaths = @(
  (Join-Path $env:USERPROFILE '.dotnet\tools'),
  (Join-Path $env:HOME '.dotnet/tools')
) | Where-Object { $_ -and (Test-Path $_) }

foreach ($tp in $toolPaths) {
  if ($env:PATH -notlike "*$tp*") { $env:PATH = "$tp;$env:PATH" }
}

# 8) Genera report HTML se abbiamo file di coverage
if ($coveragePaths) {
  Ensure-Dir $CoverageDir
  Write-Host "Generazione report da:"
  $coveragePaths | ForEach-Object { Write-Host " - $_" }
  $reportsArg = ($coveragePaths -join ';')

  & reportgenerator -reports:"$reportsArg" -targetdir:"$CoverageDir" -reporttypes:"Html;TextSummary"
  if ($LASTEXITCODE -ne 0) {
    Write-Warning "ReportGenerator ha restituito codice $LASTEXITCODE (continuo comunque)."
  } else {
    $summary = Join-Path $CoverageDir 'Summary.txt'
    if (Test-Path $summary) {
      Write-Host "=== Coverage Summary ==="
      Get-Content $summary | Write-Host
    }
  }
} else {
  Write-Warning "Coverage non trovato: salto la generazione HTML."
}

Write-Host "✅ Script completato. Risultati test in '$ResultsDir', coverage in '$CoverageDir' (se presente)."
