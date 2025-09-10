<#
.SYNOPSIS
  Lancia un workflow GitHub Actions e ne segue l'esecuzione; crea un report Markdown/JSON.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$Workflow,
  [string]$Repo,
  [string]$Ref,
  [hashtable]$Inputs,
  [switch]$DownloadArtifacts
)

function Ensure-Gh {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' non trovato. Installa da https://cli.github.com/"
  }
  gh auth status 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) {
    throw "Non autenticato. Esegui: gh auth login -h github.com -s repo,workflow,read:packages,write:packages"
  }
}

function Resolve-Repo {
  param([string]$Repo)
  if ($Repo) { return $Repo }
  if (Get-Command git -ErrorAction SilentlyContinue) {
    $url = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $url) {
      if ($url -match '[:/](?<owner>[^/]+)/(?<repo>[^/\.]+)(?:\.git)?$') {
        return ($Matches.owner + "/" + $Matches.repo)
      }
    }
  }
  throw "Impossibile determinare OWNER/REPO. Passa -Repo 'Owner/Repo'."
}

function Resolve-Ref {
  param([string]$Ref)
  if ($Ref) { return $Ref }
  if (Get-Command git -ErrorAction SilentlyContinue) {
    $branch = git rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $branch) { return $branch.Trim() }
  }
  return "main"
}

function Resolve-WorkflowSelector {
  param([string]$Repo, [string]$Workflow)
  if ($Workflow -match '\.ya?ml$') { return $Workflow }
  try {
    $json = gh workflow list --repo $Repo --json name,path | ConvertFrom-Json
    $hit = $json | Where-Object { $_.name -eq $Workflow } | Select-Object -First 1
    if ($hit) { return $hit.path }
  } catch {}
  return $Workflow
}

function Start-Workflow {
  param([string]$Repo, [string]$WorkflowSel, [string]$Ref, [hashtable]$Inputs)
  Write-Host ("Lancio workflow '{0}' su '{1}' (ref: {2})..." -f $WorkflowSel, $Repo, $Ref)
  $args = @('workflow','run', $WorkflowSel, '--repo', $Repo, '--ref', $Ref)
  if ($Inputs) {
    foreach ($k in $Inputs.Keys) { $args += @('-f', ($k + "=" + $Inputs[$k])) }
  }
  $null = & gh @args 2>&1 | Tee-Object -Variable runOut
  if ($LASTEXITCODE -ne 0) { throw ("Errore nel lancio workflow: " + $runOut) }
  Start-Sleep -Seconds 2
  $view = gh run list --repo $Repo --workflow $WorkflowSel --branch $Ref -L 1 --json databaseId,headBranch,headSha,displayTitle,status,conclusion,workflowName,createdAt,url | ConvertFrom-Json
  if (-not $view) { throw "Impossibile trovare il run appena lanciato." }
  return $view[0]
}

function Watch-Run {
  param([string]$Repo, [string]$RunUrl)
  Write-Host ("Attendo completamento run: {0}" -f $RunUrl)
  $null = & gh run watch $RunUrl --repo $Repo --exit-status 2>&1
  return $LASTEXITCODE
}

function Get-RunReport {
  param([string]$Repo, [string]$RunUrl)
  gh run view $RunUrl --repo $Repo --json databaseId,status,conclusion,startedAt,updatedAt,headSha,headBranch,workflowName,steps,jobs,artifacts | ConvertFrom-Json
}

function Save-ReportFiles {
  param([pscustomobject]$Report, [string]$Repo, [switch]$DownloadArtifacts)
  $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
  $outDir = Join-Path -Path (Get-Location) -ChildPath ("run-report-" + $Report.databaseId + "-" + $stamp)
  New-Item -ItemType Directory -Path $outDir -Force | Out-Null

  $jsonPath = Join-Path $outDir 'report.json'
  $Report | ConvertTo-Json -Depth 8 | Out-File -FilePath $jsonPath -Encoding utf8

  $logPath = Join-Path $outDir 'run.log.txt'
  gh run view $Report.databaseId --log > $logPath

  $artDir = $null
  if ($DownloadArtifacts) {
    $artDir = Join-Path $outDir 'artifacts'
    New-Item -ItemType Directory -Path $artDir -Force | Out-Null
    gh run download $Report.databaseId -D $artDir | Out-Null
  }

  $mdPath = Join-Path $outDir 'report.md'
  "# Run Report - " + $Report.workflowName | Out-File -FilePath $mdPath -Encoding utf8
  "" | Add-Content $mdPath
  ("- Repo: " + $Repo) | Add-Content $mdPath
  ("- Run ID: " + $Report.databaseId) | Add-Content $mdPath
  ("- Branch: " + $Report.headBranch) | Add-Content $mdPath
  ("- SHA: " + $Report.headSha) | Add-Content $mdPath
  ("- Status: **" + $Report.status + "**") | Add-Content $mdPath
  ("- Conclusion: **" + $Report.conclusion + "**") | Add-Content $mdPath
  ("- Started: " + $Report.startedAt) | Add-Content $mdPath
  ("- Updated: " + $Report.updatedAt) | Add-Content $mdPath
  "" | Add-Content $mdPath
  "## Jobs" | Add-Content $mdPath
  foreach ($j in $Report.jobs) {
    ("- " + $j.name + " → " + $j.conclusion) | Add-Content $mdPath
    foreach ($s in $j.steps) {
      $nm = if ($s.name) { $s.name } else { "(step)" }
      ("  - " + $nm + " → " + $s.conclusion) | Add-Content $mdPath
    }
  }

  return @{
    OutDir = $outDir
    Json = $jsonPath
    Log = $logPath
    Markdown = $mdPath
    ArtifactsDir = $artDir
  }
}

try {
  Ensure-Gh
  $Repo = Resolve-Repo -Repo $Repo
  $Ref = Resolve-Ref -Ref $Ref
  $WorkflowSel = Resolve-WorkflowSelector -Repo $Repo -Workflow $Workflow

  $run = Start-Workflow -Repo $Repo -WorkflowSel $WorkflowSel -Ref $Ref -Inputs $Inputs
  $exit = Watch-Run -Repo $Repo -RunUrl $run.url

  $report = Get-RunReport -Repo $Repo -RunUrl $run.url
  $files = Save-ReportFiles -Report $report -Repo $Repo -DownloadArtifacts:$DownloadArtifacts

  Write-Host ("Completato. Esito: " + $report.conclusion)
  Write-Host ("Report JSON: " + $files.Json)
  Write-Host ("Report MD:   " + $files.Markdown)
  if ($files.ArtifactsDir) { Write-Host ("Artifacts:    " + $files.ArtifactsDir) }
  Write-Host ("Log:          " + $files.Log)
  exit $exit
} catch {
  Write-Error $_.Exception.Message
  exit 1
}
