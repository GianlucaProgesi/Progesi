<#
  Cancella le run queued/in_progress avviate su branch "dependabot/...".
  Esclude i branch elencati in -KeepBranches.
#>
[CmdletBinding()]
param(
  [string[]]$KeepBranches = @('main','chore/ci-trx'),
  [switch]$WhatIf  # anteprima senza cancellare
)

function Ensure-Gh {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI 'gh' non trovato. Installa da https://cli.github.com/"
  }
  gh auth status 2>$null | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "Non autenticato. Esegui: gh auth login -h github.com -s repo,workflow" }
}

function Resolve-Repo {
  if (Get-Command git -ErrorAction SilentlyContinue) {
    $url = git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $url -match '[:/](?<o>[^/]+)/(?<r>[^/\.]+)(?:\.git)?$') {
      return ($Matches.o + "/" + $Matches.r)
    }
  }
  throw "Impossibile determinare OWNER/REPO. Passa manualmente con gh run list --repo <owner/repo> se serve."
}

try {
  Ensure-Gh
  $repo = Resolve-Repo

  # Prendi runs IN PROGRESS + QUEUED, usando solo campi supportati
  $json1 = gh run list --repo $repo --status in_progress --json databaseId,headBranch,workflowName,status,url,number,event
  $json2 = gh run list --repo $repo --status queued       --json databaseId,headBranch,workflowName,status,url,number,event

  $runs = @()
  if ($json1) { $runs += ($json1 | ConvertFrom-Json) }
  if ($json2) { $runs += ($json2 | ConvertFrom-Json) }

  if (-not $runs -or $runs.Count -eq 0) { Write-Host "Nessuna run in_progress/queued."; exit 0 }

  # Filtra i branch di Dependabot
  $targets = $runs | Where-Object {
    $_.headBranch -match '^dependabot/' -and ($KeepBranches -notcontains $_.headBranch)
  }

  if (-not $targets -or $targets.Count -eq 0) { Write-Host "Nessuna run di dependabot da cancellare."; exit 0 }

  foreach ($r in $targets) {
    Write-Host ("{0} | {1} | {2} | {3}" -f $r.databaseId, $r.workflowName, $r.headBranch, $r.status)
    if (-not $WhatIf) { gh run cancel $r.databaseId --repo $repo | Out-Null }
  }
  if ($WhatIf) { Write-Host "Anteprima completata. (Usa senza -WhatIf per cancellare)" }
  else { Write-Host "Cancellazione completata." }

} catch {
  Write-Error $_.Exception.Message
  exit 1
}
