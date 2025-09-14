<# 
  coverage.ps1
  Esegue i test dei due progetti, genera Cobertura (MSBuild + XPlat),
  merge con ReportGenerator e (opzionale) gate percentuale.
#>

[CmdletBinding()]
param(
  [switch]$RunTests,
  [double]$MinLine = -1
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Resolve-ScriptRoot {
  if ($PSScriptRoot) { return $PSScriptRoot }
  $p = $PSCommandPath
  if (-not $p) { try { $p = $MyInvocation.MyCommand.Path } catch {} }
  if ($p) { return (Split-Path -Parent $p) }
  return (Get-Location | Select-Object -ExpandProperty Path)
}

function Resolve-FullPath([string]$path) {
  (Resolve-Path -LiteralPath $path -ErrorAction Stop | Select-Object -First 1 -ExpandProperty Path)
}

function Write-Title([string]$text) {
  Write-Host ""
  Write-Host ">> $text"
}

function Find-CoverageFile([string]$projectPath) {
  $proj = Resolve-FullPath $projectPath
  $dir  = Split-Path -Parent $proj

  $msbuild = Join-Path $dir 'TestResults/Coverage/coverage.cobertura.xml'
  if (Test-Path $msbuild) { return (Resolve-FullPath $msbuild) }

  $testRes = Join-Path $dir 'TestResults'
  if (Test-Path $testRes) {
    $xplat = Get-ChildItem -Path $testRes -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue `
            | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($xplat) { return $xplat.FullName }
  }
  return $null
}

function Invoke-Tests([string]$projectPath) {
  $proj = Resolve-FullPath $projectPath
  $dir  = Split-Path -Parent $proj

  Write-Title "dotnet test ($proj) con coverlet (Cobertura + XPlat)..."

  $args = @(
    'test', $proj, '-c','Debug',
    '/p:CollectCoverage=true',
    '/p:CoverletOutput=TestResults/Coverage/',
    '/p:CoverletOutputFormat=cobertura',
    '--collect:"XPlat Code Coverage"'
  )

  # Stampa a video ma non “entra” nel pipeline dello script
  $null = (& dotnet @args 2>&1 | Out-Host)

  $covMsbuild = Join-Path $dir 'TestResults/Coverage/coverage.cobertura.xml'
  $testRes    = Join-Path $dir 'TestResults'
  $covXplat   = $null
  if (Test-Path $testRes) {
    $covXplat = Get-ChildItem -Path $testRes -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue `
               | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  }

  $paths = @()
  if (Test-Path $covMsbuild) { $paths += (Resolve-FullPath $covMsbuild) }
  if ($covXplat)              { $paths += $covXplat.FullName }

  if ($paths.Count -eq 0) {
    Write-Host "Contenuto ${testRes}:"
    if (Test-Path $testRes) { Get-ChildItem -Recurse -Path $testRes | ForEach-Object { $_.FullName } | Out-Host }
    throw "Cobertura non trovati per $proj."
  }

  return $paths[0]  # priorità a MSBuild, fallback XPlat
}

function Merge-Coverage([string[]]$reportFiles, [string]$targetDir) {
  if (-not (Test-Path $targetDir)) { $null = New-Item -ItemType Directory -Force -Path $targetDir }
  if (-not $reportFiles -or $reportFiles.Count -lt 1) { throw "Nessun report da mergiare." }

  # reportgenerator accetta lista separata da ';'
  $reportsArg = ($reportFiles | ForEach-Object { $_.Replace('\','/') }) -join ';'

  Write-Title "Merge coverage con ReportGenerator..."
  & reportgenerator `
      ("-reports:{0}" -f $reportsArg) `
      ("-targetdir:{0}" -f $targetDir) `
      "-reporttypes:Cobertura;TextSummary" `
      2>&1 | Out-Host

  $cob = Join-Path $targetDir 'Cobertura.xml'
  if (!(Test-Path $cob)) { throw "Report merge non creato ($cob)." }

  return $cob
}

function Show-Summary([string]$mergedDir) {
  $summary = Join-Path $mergedDir 'Summary.txt'
  $cob     = Join-Path $mergedDir 'Cobertura.xml'

  Write-Host ""
  Write-Host "### Test coverage"
  Write-Host "Report: $cob"
  Write-Host ""

  if (Test-Path $summary) { Get-Content $summary | Out-Host }
  else { Write-Host "(Summary non trovato, mostro solo path Cobertura)" }
}

# ----- MAIN -----

$here = Resolve-ScriptRoot
$root = Resolve-FullPath (Join-Path $here '..')

$projCore   = Join-Path $root 'tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj'
$projSqlite = Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/ProgesiRepositories.Sqlite.Tests.csproj'

$covCore   = $null
$covSqlite = $null

if ($RunTests) {
  $covCore   = Invoke-Tests $projCore
  $covSqlite = Invoke-Tests $projSqlite
} else {
  $covCore   = Find-CoverageFile $projCore
  $covSqlite = Find-CoverageFile $projSqlite
}

if (-not $covCore -or -not $covSqlite) {
  throw "Cobertura non trovati. Core: '$covCore' esiste: $([bool]$covCore); Sqlite: '$covSqlite' esiste: $([bool]$covSqlite)"
}

$mergedDir = Join-Path $root 'TestResults/MergedCoverage'
$null = Merge-Coverage @($covCore, $covSqlite) $mergedDir

Show-Summary $mergedDir

if ($MinLine -ge 0) {
  $summary = Join-Path $mergedDir 'Summary.txt'
  if (!(Test-Path $summary)) { throw "Summary non trovato: $summary" }

  $txt = Get-Content $summary -Raw
  $m = [regex]::Match($txt, 'Line coverage:\s*([\d\.,]+)%', 'IgnoreCase')
  if (-not $m.Success) { throw "Impossibile leggere la Line coverage dal summary." }

  $pct = ($m.Groups[1].Value.Replace(',','.') -as [double])

  Write-Host ""
  if ($pct -lt $MinLine) {
    throw ("Gate FALLITO: coverage {0:N2}% < soglia {1}%" -f $pct, $MinLine)
  } else {
    Write-Host ("Gate OK: coverage {0:N2}% >= soglia {1}%." -f $pct, $MinLine)
  }
}
