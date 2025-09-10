param(
  [switch]$NormalizeEol,
  [switch]$VerifyFormat,
  [switch]$OpenReport,
  [string]$Solution
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-Tool {
  param(
    [Parameter(Mandatory)] [string]$Cmd,
    [Parameter(Mandatory)] [string[]]$Args
  )
  Write-Host ">> $Cmd $($Args -join ' ')"
  & $Cmd @Args
  if ($LASTEXITCODE -ne 0) { throw "$Cmd failed with exit code $LASTEXITCODE" }
}

function Resolve-RepoRoot {
  try { (git rev-parse --show-toplevel) 2>$null } catch { $null }
}

function Resolve-Solution {
  param([string]$Explicit)
  if ($Explicit) {
    $p = Resolve-Path $Explicit -ErrorAction Stop
    return $p.Path
  }
  $root = Get-Location

  $slnf = @(Get-ChildItem -Path $root -Recurse -File -Filter *.slnf | Sort-Object FullName)
  if ($slnf.Length -gt 0) { return $slnf[0].FullName }

  $progesi = @(Get-ChildItem -Path $root -Recurse -File -Filter Progesi.sln | Select-Object -First 1)
  if ($progesi.Length -gt 0) { return $progesi[0].FullName }

  $all = @(Get-ChildItem -Path $root -Recurse -File -Filter *.sln | Sort-Object FullName)
  if     ($all.Length -eq 1) { return $all[0].FullName }
  elseif ($all.Length -gt 1) { return $all[0].FullName }

  throw "Nessuna soluzione .sln/.slnf trovata sotto $root."
}

# Root repo
$gitRoot = Resolve-RepoRoot
if (-not $gitRoot) { $gitRoot = Resolve-Path "$PSScriptRoot\.." }
Set-Location $gitRoot
Write-Host "Repo root: $gitRoot"

# Workspace
$workspace = Resolve-Solution -Explicit $Solution
Write-Host "Workspace: $workspace (passa -Solution <path> per override)"

# 1) Normalizza EOL
if ($NormalizeEol) {
  Write-Host "Normalizzo EOL (CRLF) per *.cs, *.csproj, *.sln, *.props, *.targets, *.ps1..."
  git config core.autocrlf true | Out-Null
  $patterns = @('*.cs','*.csproj','*.sln','*.props','*.targets','*.ps1')
  $files = @()
  foreach ($pat in $patterns) { $files += @(Get-ChildItem -Recurse -File -Filter $pat) }
  foreach ($f in $files) {
    $p = $f.FullName
    $txt = Get-Content -Raw -LiteralPath $p
    $txt = $txt -replace "(`r)?`n","`r`n"
    [System.IO.File]::WriteAllText($p, $txt, [System.Text.UTF8Encoding]::new($false))
  }
}

# 2) dotnet format
Invoke-Tool -Cmd 'dotnet' -Args @('format', $workspace, '--verbosity','minimal')
if ($VerifyFormat) {
  Invoke-Tool -Cmd 'dotnet' -Args @('format', $workspace, '--verify-no-changes','--verbosity','minimal')
}

# 3) Restore & Build
Invoke-Tool -Cmd 'dotnet' -Args @('restore', $workspace)
Invoke-Tool -Cmd 'dotnet' -Args @('build',   $workspace, '--configuration','Release','--no-restore')

# 4) Test con coverage
$resultsDir = "TestResults"
if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$runsettings = "tests/coverlet.runsettings"
$settingsArgs = @()
if (Test-Path $runsettings) { $settingsArgs = @('--settings', $runsettings) }

Invoke-Tool -Cmd 'dotnet' -Args (@('test', $workspace, '--configuration','Release','--no-build','--collect','XPlat Code Coverage','--results-directory',$resultsDir) + $settingsArgs)

# 5) ReportGenerator
$toolDir = Join-Path $env:USERPROFILE ".dotnet\tools"
$reportCmd = "reportgenerator"
if (-not (Get-Command $reportCmd -ErrorAction SilentlyContinue)) {
  Invoke-Tool -Cmd 'dotnet' -Args @('tool','update','-g','dotnet-reportgenerator-globaltool')
  $env:PATH = "$toolDir;$env:PATH"
}

$reports = @(Get-ChildItem -Recurse -Path $resultsDir -Filter "coverage.cobertura.xml" | ForEach-Object { $_.FullName })
if ($reports.Length -eq 0) { throw "Nessun coverage.cobertura.xml trovato sotto $resultsDir." }

Write-Host (Get-Date -Format o) ": Arguments"
Write-Host " -reports:$($reports -join ';')"
Write-Host " -targetdir:CoverageReport"
Write-Host " -reporttypes:Html;TextSummary;Cobertura"
Write-Host " -assemblyfilters:+ProgesiCore"
Write-Host " -classfilters:-*Logger*;-*LegacyExtensions*;-*TraceLogger*"

# ⚠️ FIX: usa sintassi -key:value (non separare con spazi)
$rgArgs = @(
  "-reports:$($reports -join ';')",
  "-targetdir:CoverageReport",
  "-reporttypes:Html;TextSummary;Cobertura",
  "-assemblyfilters:+ProgesiCore",
  "-classfilters:-*Logger*;-*LegacyExtensions*;-*TraceLogger*"
)
Invoke-Tool -Cmd $reportCmd -Args $rgArgs

# 6) Sommario + apri report
$summ = "CoverageReport\Summary.txt"
if (Test-Path $summ) {
  Write-Host "`n----- Coverage Summary (ProgesiCore only) -----"
  Get-Content $summ | Write-Host
} else {
  Write-Warning "Coverage summary non trovato."
}

if ($OpenReport) {
  $idx = "CoverageReport\index.html"
  if (Test-Path $idx) { Start-Process $idx }
}

# 7) Analisi “buchi” (facoltativa)
$uncovered = "tools\Get-UncoveredCoverage.ps1"
if (Test-Path $uncovered) {
  Write-Host "`n— Analisi linee scoperte (Top 20, focus classi principali) —"
  pwsh -File $uncovered -ClassFilter "ProgesiMetadata|ProgesiSnip|ValueObject" -Top 20
}
