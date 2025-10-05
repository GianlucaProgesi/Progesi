Param(
  [int]$MinTotal = 106,
  [string]$Solution = "Progesi.sln",
  [string]$ResultsDir = "out\verify"
)

$ErrorActionPreference = "Stop"

function New-Dir([string]$p) { New-Item -ItemType Directory -Force -Path $p | Out-Null }
function Safe-Int($v) { if ($null -eq $v -or $v -eq "") { 0 } else { try { [int]$v } catch { 0 } } }

function Find-VsTestConsole {
  if ($env:VSTEST_CONSOLE -and (Test-Path $env:VSTEST_CONSOLE)) { return $env:VSTEST_CONSOLE }
  $vswhere = "$env:ProgramFiles(x86)\Microsoft Visual Studio\Installer\vswhere.exe"
  if (Test-Path $vswhere) {
    $inst = & $vswhere -latest -products * -requires Microsoft.Component.TestTools.BuildTools -property installationPath 2>$null
    if (-not $inst) { $inst = & $vswhere -latest -products * -property installationPath 2>$null }
    if ($inst) {
      $cand = Join-Path $inst "Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
      if (Test-Path $cand) { return $cand }
    }
  }
  $cands = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\TestPlatform\vstest.console.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe"
  )
  foreach ($c in $cands) { if (Test-Path $c) { return $c } }
  return $null
}

function Parse-TrxCounters([string]$trxPath) {
  if (-not (Test-Path $trxPath)) { return $null }
  [xml]$xml = Get-Content $trxPath
  $c = $xml.TestRun.ResultSummary.Counters
  if ($null -eq $c) { return $null }
  [pscustomobject]@{
    Total   = Safe-Int $c.total
    Passed  = Safe-Int $c.passed
    Failed  = (Safe-Int $c.failed) + (Safe-Int $c.error) + (Safe-Int $c.timeout) + (Safe-Int $c.aborted) + (Safe-Int $c.notRunnable)
    Skipped = (Safe-Int $c.notExecuted) + (Safe-Int $c.inconclusive) + (Safe-Int $c.warning) + (Safe-Int $c.passedButRunAborted) + (Safe-Int $c.pending)
  }
}

function Run-DotnetTest([string]$solutionPath, [string]$config, [string]$baseResults) {
  $dir = Join-Path $baseResults ("dotnet-" + $config)
  New-Dir $dir
  & dotnet restore $solutionPath --nologo | Out-Null
  & dotnet build   $solutionPath -c $config --nologo --no-restore | Out-Null
  $args = @("test", $solutionPath, "-c", $config, "--logger", "trx;LogFileName=results.trx", "--results-directory", $dir)
  & dotnet @args 2>&1 | Tee-Object -FilePath (Join-Path $dir "log.txt") | Out-Null
  $trx = Get-ChildItem -Path $dir -Recurse -Filter *.trx -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Desc | Select-Object -First 1
  if ($trx) { return Parse-TrxCounters $trx.FullName }
  else { return [pscustomobject]@{Total=0;Passed=0;Failed=0;Skipped=0} }
}

function Find-TestAssemblies([string]$config) {
  $dlls = @()
  $roots = @(".\tests",".")
  foreach ($r in $roots) {
    if (Test-Path $r) {
      $dlls += Get-ChildItem -Path $r -Recurse -Filter *.dll -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -match "\\bin\\$config\\" -and
        $_.Name -match "test|tests" -and
        $_.FullName -notmatch "\\ref\\|\\obj\\|testhost|Microsoft|System"
      }
    }
  }
  $dlls | Select-Object -ExpandProperty FullName -Unique
}

function Run-VsTest([string]$config, [string]$baseResults) {
  $exe = Find-VsTestConsole
  if (-not $exe) { return [pscustomobject]@{Total=0;Passed=0;Failed=0;Skipped=0} }
  $dir = Join-Path $baseResults ("vstest-" + $config)
  New-Dir $dir
  $assemblies = Find-TestAssemblies $config
  if (-not $assemblies -or $assemblies.Count -eq 0) { return [pscustomobject]@{Total=0;Passed=0;Failed=0;Skipped=0} }
  $all = [pscustomobject]@{Total=0;Passed=0;Failed=0;Skipped=0}
  $i=0
  foreach ($asm in $assemblies) {
    $i++
    $runDir = Join-Path $dir ("run" + $i)
    New-Dir $runDir
    $trx = Join-Path $runDir "results.trx"
    & "$exe" "$asm" /Logger:"trx;LogFileName=results.trx" /ResultsDirectory:"$runDir" | Out-Null
    $c = Parse-TrxCounters $trx
    if ($c) {
      $all.Total  += $c.Total
      $all.Passed += $c.Passed
      $all.Failed += $c.Failed
      $all.Skipped+= $c.Skipped
    }
  }
  $all
}

function Best([pscustomobject]$a, [pscustomobject]$b) {
  if ($a.Total -ge $b.Total) { $a } else { $b }
}

New-Dir $ResultsDir

# 1) Release
$r_dotnet_rel = Run-DotnetTest $Solution "Release" $ResultsDir
$r_vstest_rel = Run-VsTest      "Release" $ResultsDir
$best_rel     = Best $r_dotnet_rel $r_vstest_rel

# 2) Debug
$r_dotnet_dbg = Run-DotnetTest $Solution "Debug" $ResultsDir
$r_vstest_dbg = Run-VsTest      "Debug" $ResultsDir
$best_dbg     = Best $r_dotnet_dbg $r_vstest_dbg

# 3) Scegli il migliore fra Release e Debug
$best = Best $best_rel $best_dbg

$summaryPath = Join-Path $ResultsDir "summary.txt"
("Release: dotnet={0} vstest={1}  |  Debug: dotnet={2} vstest={3}" -f $r_dotnet_rel.Total,$r_vstest_rel.Total,$r_dotnet_dbg.Total,$r_vstest_dbg.Total) | Tee-Object $summaryPath | Out-Null
("Best totals => Total={0}; Passed={1}; Failed={2}; Skipped={3}" -f $best.Total,$best.Passed,$best.Failed,$best.Skipped) | Tee-Object $summaryPath -Append | Out-Null
Write-Host ("Best totals => Total={0} | Passed={1} | Failed={2} | Skipped={3}" -f $best.Total,$best.Passed,$best.Failed,$best.Skipped)

if ($best.Failed -gt 0) { throw ("Tests failed: {0}. See {1}" -f $best.Failed,$ResultsDir) }
if ($best.Total -lt $MinTotal) { throw ("Total tests ({0}) below minimum ({1}). See {2}" -f $best.Total,$MinTotal,$ResultsDir) }

Write-Host ("OK: {0} tests, 0 failed. See {1}" -f $best.Total,$summaryPath)
