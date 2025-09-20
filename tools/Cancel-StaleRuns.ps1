# tools/Cancel-StaleRuns.ps1
[CmdletBinding()]
param(
  [string]$Owner = "GianlucaProgesi",
  [string]$Repo  = "Progesi",
  [string]$KeepRef = "fix/actions-jungle"   # branch della PR da tenere
)

$ErrorActionPreference = "Stop"

Write-Host "== Cancel stale Actions runs (keep latest per workflow on $KeepRef) ==" -ForegroundColor Cyan

# scarica gli ultimi 200 run (puoi aumentare se serve)
$runs = gh api "repos/$Owner/$Repo/actions/runs?per_page=200" | ConvertFrom-Json
if (-not $runs.workflow_runs) { Write-Host "Nessun run trovato." ; exit 0 }

# partizioniamo per (workflow_id, head_branch)
$groups = @{}
foreach ($r in $runs.workflow_runs) {
  $key = "{0}|{1}" -f $r.workflow_id, $r.head_branch
  if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
  $groups[$key] += $r
}

$toCancel = @()

foreach ($kvp in $groups.GetEnumerator()) {
  $key = $kvp.Key
  $arr = $kvp.Value | Sort-Object -Property created_at -Descending

  $wfId, $branch = $key -split "\|", 2

  # tieni solo l'ultimissimo run del branch target (KeepRef)
  # per TUTTI gli altri branch/PR cancella tutto (queued/in_progress)
  for ($i=0; $i -lt $arr.Count; $i++) {
    $r = $arr[$i]
    $isLatestOnKeep = ($branch -eq $KeepRef -and $i -eq 0)
    if ($isLatestOnKeep) { continue }

    if ($r.status -in @("queued","in_progress")) {
      $toCancel += $r
    }
  }
}

if ($toCancel.Count -eq 0) {
  Write-Host "Niente da cancellare." -ForegroundColor Green
  exit 0
}

Write-Host "Cancello $($toCancel.Count) run..." -ForegroundColor Yellow
foreach ($r in $toCancel) {
  $id = $r.id
  $name = $r.name
  $branch = $r.head_branch
  $status = $r.status
  Write-Host (" - {0}  (wf='{1}', branch='{2}', status={3})" -f $id, $name, $branch, $status)
  gh run cancel $id | Out-Null
}

Write-Host "== DONE ==" -ForegroundColor Cyan
