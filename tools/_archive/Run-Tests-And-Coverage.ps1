<#
.SYNOPSIS
  Restore+build+test con XPlat coverage, summary (Text/HTML), confronto baseline.
  Pulisce TestResults e CoverageReport e genera il report SOLO dal coverage più recente.
#>

[CmdletBinding()]
param(
  [string]$TestProject   = "tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj",
  [string]$Solution      = "",
  [ValidateSet("Release","Debug")]
  [string]$Configuration = "Release",
  [string]$BaselinePath  = "tests/coverage-baseline.json",
  [string]$RunSettingsPath = "tests/coverage.runsettings",
  [switch]$Strict,
  [double]$Tolerance = 0.05,
  [switch]$OpenSummary
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
  param([Parameter(Mandatory=$true)][string]$Exe,[string[]]$Args = @(),[switch]$ReturnOutput)
  Write-Host ">> $Exe $($Args -join ' ')" -ForegroundColor Cyan
  $global:LASTEXITCODE = 0
  $out = & $Exe @Args 2>&1
  $code = $LASTEXITCODE
  if ($code -ne 0) { if ($out) { Write-Host ($out | Out-String) -ForegroundColor DarkGray }; throw "Command failed with exit code ${code}: $Exe $($Args -join ' ')" }
  if ($ReturnOutput) { return ($out | Out-String) }
}

function Ensure-ReportGenerator {
  $rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
  if ($rg) { return $rg.Path }
  $candidate = Join-Path $env:USERPROFILE ".dotnet\tools\reportgenerator.exe"
  if (Test-Path $candidate) { return $candidate }
  Write-Host "Installo dotnet-reportgenerator-globaltool..." -ForegroundColor Yellow
  Invoke-Checked -Exe "dotnet" -Args @("tool","install","-g","dotnet-reportgenerator-globaltool")
  if (Test-Path $candidate) { return $candidate }
  $rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
  if ($rg) { return $rg.Path }
  throw "Impossibile trovare/installare reportgenerator."
}

function Ensure-RunSettings {
  param([string]$Path)
  if (Test-Path $Path) { return }
  Write-Host "Creo $Path (default filters)…" -ForegroundColor Yellow
  New-Item -ItemType Directory -Force -Path (Split-Path $Path -Parent) | Out-Null
  @'
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Include>
            <ModulePath>.*ProgesiCore\.dll$</ModulePath>
            <ModulePath>.*ProgesiRepositories\.Sqlite\.dll$</ModulePath>
          </Include>
          <Exclude>
            <Source>.*\\ProgesiRepositories\.Sqlite\\.*Logger\.cs$</Source>
            <Source>.*\\ProgesiRepositories\.Sqlite\\SqliteVariableRepositoryLegacyExtensions\.cs$</Source>
          </Exclude>
          <ExcludeByAttribute>System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
'@ | Set-Content -Path $Path -Encoding UTF8 -NoNewline
}

function Get-LatestCobertura {
  $files = Get-ChildItem -Recurse -File -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTimeUtc -Descending
  if ($files -and $files.Count -gt 0) { return $files[0] }
  return $null
}

try {
  # Root repo
  $repoRoot = (Invoke-Checked -Exe "git" -Args @("rev-parse","--show-toplevel") -ReturnOutput).Trim()
  if (-not $repoRoot) { throw "Non sembra una repo Git." }
  Set-Location $repoRoot
  Write-Host "Repo: $repoRoot" -ForegroundColor DarkGray

  # Assicura il runsettings
  Ensure-RunSettings -Path $RunSettingsPath

  # Restore
  if ($Solution) { Invoke-Checked -Exe "dotnet" -Args @("restore", $Solution) }
  else { Invoke-Checked -Exe "dotnet" -Args @("restore", $TestProject) }

  # Build
  Invoke-Checked -Exe "dotnet" -Args @("build", $TestProject, "-c", $Configuration, "--no-restore")

  # Pulisci TestResults e CoverageReport per evitare MultiReport e residui
  Get-ChildItem -Path "tests" -Recurse -Directory -Filter "TestResults" -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
  if (Test-Path "CoverageReport") { Remove-Item -Recurse -Force "CoverageReport" -ErrorAction SilentlyContinue }

  # Test + coverage + runsettings
  $trxName = "Local.trx"
  Invoke-Checked -Exe "dotnet" -Args @(
    "test", $TestProject,
    "-c", $Configuration,
    "--no-build",
    "--collect:XPlat Code Coverage",
    "--settings", $RunSettingsPath,
    "-l", "trx;LogFileName=$trxName"
  )

  # Individua SOLO il file più recente
  $latest = Get-LatestCobertura
  if (-not $latest) {
    Write-Warning "Nessun file coverage trovato. Hai aggiunto 'coverlet.collector' al progetto di test?"
    Write-Host "Esempio: dotnet add $TestProject package coverlet.collector --version 6.0.0" -ForegroundColor DarkGray
    exit 0
  }
  Write-Host ("Coverage file (latest): {0}" -f $latest.FullName) -ForegroundColor Yellow

  # ReportGenerator: usa SOLO quel file (niente glob)
  $rgPath = Ensure-ReportGenerator
  New-Item -ItemType Directory -Force -Path "CoverageReport" | Out-Null
  Invoke-Checked -Exe $rgPath -Args @(
    "-reports:$($latest.FullName)",
    "-targetdir:CoverageReport",
    "-reporttypes:TextSummary;HtmlSummary;XmlSummary"
  )

  if (Test-Path "CoverageReport/Summary.txt") {
    Write-Host "`n=== Coverage Summary (Text) ===" -ForegroundColor Green
    Get-Content "CoverageReport/Summary.txt"
  } else {
    Write-Warning "Summary.txt non trovato."
  }

  # Confronto baseline (se presente)
  $compareScript = "tools/Compare-Coverage.ps1"
  if (Test-Path $compareScript) {
    $args = @("-File", $compareScript, "-BaselinePath", $BaselinePath, "-Tolerance", $Tolerance)
    if ($Strict) { $args += "-Strict" } else { $args += "-WarnOnly" }
    Invoke-Checked -Exe "pwsh" -Args $args
  } else {
    Write-Warning "Compare-Coverage.ps1 non trovato; salto il confronto."
  }

  if ($OpenSummary -and (Test-Path "CoverageReport/Summary.html")) {
    Write-Host "Apro CoverageReport/Summary.html..." -ForegroundColor Yellow
    Start-Process "CoverageReport/Summary.html" | Out-Null
  }

  Write-Host "`nCompletato." -ForegroundColor Green
}
catch { Write-Error $_.Exception.Message; exit 1 }
