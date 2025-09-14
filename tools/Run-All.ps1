<# orchestratore locale+remoto #>
param(
  [int]$MinLine = 75,
  [switch]$LocalOnly,
  [switch]$RemoteOnly,
  [switch]$Push
)
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
  try { (git rev-parse --show-toplevel) | Resolve-Path } catch { Resolve-Path "$PSScriptRoot/.." }
}
$root = (Get-RepoRoot).Path
$branch = (git rev-parse --abbrev-ref HEAD).Trim()

function Ensure-ReportGenerator {
  try { reportgenerator -? > $null 2>&1 } catch {
    dotnet tool install -g dotnet-reportgenerator-globaltool | Out-Null
    $toolPath = Join-Path $env:USERPROFILE ".dotnet\tools"
    if ($env:PATH -notlike "*$toolPath*") { $env:PATH = "$toolPath;$env:PATH" }
  }
}
function Ensure-GhCli {
  try { gh --version *> $null } catch { throw "Installa GitHub CLI: https://cli.github.com/" }
  try { gh auth status --show-token *> $null } catch { throw "Esegui 'gh auth login'." }
}

if (-not $RemoteOnly) {
  Ensure-ReportGenerator
  Write-Host ">> Eseguo test locali + coverage..." -ForegroundColor Cyan
  pwsh "$root/tools/coverage.ps1" -RunTests
  Write-Host ">> Applico gate coverage (soglia: $MinLine%)..." -ForegroundColor Cyan
  pwsh "$root/tools/coverage.ps1" -MinLine $MinLine
  Write-Host ">> Hotspots (Top 20)..." -ForegroundColor Cyan
  pwsh "$root/tools/coverage-hotspots.ps1" -Cobertura "$root/TestResults/MergedCoverage/Cobertura.xml" -Top 20
}

if (-not $LocalOnly) {
  Ensure-GhCli

  if ($Push) {
    Write-Host ">> Empty commit per triggerare TUTTI i workflow su '$branch'..." -ForegroundColor Cyan
    git commit --allow-empty -m "ci: retrigger all workflows" | Out-Null
    git push origin $branch      | Out-Null
  } else {
    Write-Host ">> Triggero SOLO build-test-coverage su '$branch'..." -ForegroundColor Cyan
    gh workflow run ".github/workflows/build-test-coverage.yml" --ref $branch | Out-Null
  }

  Write-Host ">> Monitoro i run della branch '$branch'..." -ForegroundColor Cyan
  $targets = @("build-test-coverage","CI","CodeQL","PR Labeler","Semantic PR")

  do {
    Start-Sleep -Seconds 10
    $runs = gh run list --limit 200 --json databaseId,headBranch,workflowName,status,conclusion,url `
            | ConvertFrom-Json `
            | Where-Object { $_.headBranch -eq $branch -and $_.workflowName -in $targets }

    $pending = $runs | Where-Object { $_.status -in @("queued","in_progress") }
    $failed  = $runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -ne "success" }
    $ok      = $runs | Where-Object { $_.status -eq "completed" -and $_.conclusion -eq "success" }

    Write-Host ("   Completed OK: {0}  |  Pending: {1}  |  Failed: {2}" -f ($ok|Measure-Object).Count, ($pending|Measure-Object).Count, ($failed|Measure-Object).Count)
  } while ($pending)

  if ($failed) {
    Write-Host "`n❌ Alcuni workflow sono falliti:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host (" - {0}  ->  {1}" -f $_.workflowName, $_.url) }
    throw "Workflow falliti."
  } else {
    Write-Host "`n✅ Tutti i workflow monitorati sono verdi sulla branch '$branch'." -ForegroundColor Green
  }
}

Write-Host "`nDone." -ForegroundColor Green
