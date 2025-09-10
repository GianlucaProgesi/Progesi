<#
.SYNOPSIS
  Diagnostica repository: workflows, ultimi run, ultimo run fallito (con log).
#>
[CmdletBinding()]
param(
  [string]$Repo,
  [int]$Recent = 10,
  [int]$PerWorkflow = 2,
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

try {
  Ensure-Gh
  $Repo = Resolve-Repo -Repo $Repo

  $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
  $outDir = Join-Path (Get-Location) ("diagnostics-" + $stamp)
  New-Item -ItemType Directory -Path $outDir -Force | Out-Null

  $repoJson = gh repo view $Repo --json name,owner,description,defaultBranchRef,isPrivate,sshUrl,httpUrl,visibility | ConvertFrom-Json
  $wfs = gh workflow list --repo $Repo --json id,name,path,state,url | ConvertFrom-Json
  $runs = gh run list --repo $Repo -L $Recent --json databaseId,displayTitle,workflowName,headBranch,headSha,status,conclusion,createdAt,updatedAt,url | ConvertFrom-Json

  $perWf = @()
  foreach ($wf in $wfs) {
    try {
      $rr = gh run list --repo $Repo --workflow $wf.path -L $PerWorkflow --json databaseId,displayTitle,headBranch,status,conclusion,createdAt,updatedAt,url | ConvertFrom-Json
      foreach ($r in $rr) {
        $obj = [pscustomobject]@{
          workflow = $wf.name
          path     = $wf.path
          id       = $r.databaseId
          title    = $r.displayTitle
          branch   = $r.headBranch
          status   = $r.status
          concl    = $r.conclusion
          created  = $r.createdAt
          updated  = $r.updatedAt
          url      = $r.url
        }
        $perWf += $obj
      }
    } catch {}
  }

  $failed = $runs | Where-Object { $_.conclusion -eq 'failure' -or $_.status -eq 'failure' } | Select-Object -First 1
  $failedReport = $null
  $logPath = $null
  $artDir = $null

  if ($failed) {
    $failedReport = gh run view $failed.databaseId --repo $Repo --json databaseId,status,conclusion,startedAt,updatedAt,headSha,headBranch,workflowName,steps,jobs,artifacts | ConvertFrom-Json

    $runDir = Join-Path $outDir ("failed-run-" + $failed.databaseId)
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    $logPath = Join-Path $runDir 'run.log.txt'
    gh run view $failed.databaseId --repo $Repo --log > $logPath

    if ($DownloadArtifacts) {
      $artDir = Join-Path $runDir 'artifacts'
      New-Item -ItemType Directory -Path $artDir -Force | Out-Null
      gh run download $failed.databaseId --repo $Repo -D $artDir | Out-Null
    }
  }

  $bundle = [pscustomobject]@{
    repo = $repoJson
    workflows = $wfs
    recentRuns = $runs
    runsPerWorkflow = $perWf
    lastFailed = $failed
    lastFailedReport = $failedReport
  }
  $jsonPath = Join-Path $outDir 'diagnostics.json'
  $bundle | ConvertTo-Json -Depth 8 | Out-File -FilePath $jsonPath -Encoding utf8

  $mdPath = Join-Path $outDir 'diagnostics.md'
  "# Progesi Diagnostics " + $stamp | Out-File -FilePath $mdPath -Encoding utf8
  "" | Add-Content $mdPath
  "## Repo" | Add-Content $mdPath
  ("- " + $repoJson.owner.login + "/" + $repoJson.name + " (" + $repoJson.visibility + ")") | Add-Content $mdPath
  ("- Default branch: " + $repoJson.defaultBranchRef.name) | Add-Content $mdPath
  ("- URL: " + $repoJson.httpUrl) | Add-Content $mdPath
  "" | Add-Content $mdPath

  "## Workflows" | Add-Content $mdPath
  foreach ($wf in $wfs) {
    ("- " + $wf.name + " — " + $wf.state + " — `" + $wf.path + "`") | Add-Content $mdPath
  }
  "" | Add-Content $mdPath

  "## Recent runs (global)" | Add-Content $mdPath
  foreach ($r in $runs) {
    $concl = if ($r.conclusion) { $r.conclusion } else { $r.status }
    ("- [" + $r.databaseId + "] " + $r.workflowName + " | " + $r.displayTitle + " | " + $r.headBranch + " → **" + $concl + "** (" + $r.updatedAt + ")") | Add-Content $mdPath
  }
  "" | Add-Content $mdPath

  "## Runs per workflow (ultimi)" | Add-Content $mdPath
  $by = $perWf | Group-Object workflow
  foreach ($g in $by) {
    "" | Add-Content $mdPath
    ("### " + $g.Name) | Add-Content $mdPath
    foreach ($r in $g.Group) {
      $concl2 = if ($r.concl) { $r.concl } else { $r.status }
      ("- [" + $r.id + "] " + $r.title + " | " + $r.branch + " → **" + $concl2 + "** (" + $r.updated + ")") | Add-Content $mdPath
    }
  }

  if ($failed) {
    "" | Add-Content $mdPath
    "## Ultimo run fallito" | Add-Content $mdPath
    ("- ID: " + $failed.databaseId + " — " + $failed.workflowName + " — " + $failed.displayTitle) | Add-Content $mdPath
    ("- Branch: " + $failed.headBranch) | Add-Content $mdPath
    ("- Esito: **" + $failed.conclusion + "**") | Add-Content $mdPath
    if ($logPath) { ("- Log: " + (Split-Path -Leaf $logPath)) | Add-Content $mdPath }
    if ($artDir) { ("- Artifacts: " + (Split-Path -Leaf $artDir)) | Add-Content $mdPath }
    if ($failedReport -and $failedReport.jobs) {
      "" | Add-Content $mdPath
      "### Jobs e step" | Add-Content $mdPath
      foreach ($j in $failedReport.jobs) {
        ("- " + $j.name + " → " + $j.conclusion) | Add-Content $mdPath
        foreach ($s in $j.steps) {
          $nm = if ($s.name) { $s.name } else { "(step)" }
          ("  - " + $nm + " → " + $s.conclusion) | Add-Content $mdPath
        }
      }
    }
  }

  Write-Host ("Report generato: " + $mdPath)
  Write-Host ("Bundle JSON:     " + $jsonPath)
  if ($logPath) { Write-Host ("Log fallito:     " + $logPath) }
  if ($artDir) { Write-Host ("Artifacts:       " + $artDir) }
} catch {
  Write-Error $_.Exception.Message
  exit 1
}
