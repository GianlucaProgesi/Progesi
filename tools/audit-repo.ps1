# tools/audit-repo.ps1
[CmdletBinding(SupportsShouldProcess)]
param(
  [string]$Owner,                          # opzionale: se omesso prova a dedurlo da git
  [string]$Repo,                           # opzionale
  [string]$Branch,                         # branch su cui controllare l’ultimo run dei workflow
  [int]$DaysStale = 90,                    # soglia "vecchio" per i branch remoti
  [string]$OutDir = $(Join-Path "out" ("audit-" + (Get-Date -Format "yyyyMMdd-HHmmss")))
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Assert-Cli([string]$exe) {
  if (-not (Get-Command $exe -ErrorAction SilentlyContinue)) {
    throw "Comando richiesto non trovato: $exe"
  }
}

Assert-Cli git
Assert-Cli gh

# --- deduci owner/repo/branch se non passati ---
if (-not $Branch) { $Branch = git branch --show-current }
try {
  $remoteUrl = git remote get-url origin
  if (-not $Owner -or -not $Repo) {
    if ($remoteUrl -match "github\.com[:/](.+?)/(.+?)(\.git)?$") {
      if (-not $Owner) { $Owner = $Matches[1] }
      if (-not $Repo)  { $Repo  = $Matches[2] }
    }
  }
} catch { }

if (-not $Owner -or -not $Repo) { throw "Impossibile dedurre Owner/Repo. Passali esplicitamente." }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Repo: $Owner/$Repo | Branch: $Branch | Out: $OutDir"

# --- default branch ---
$repoInfo = gh repo view "$Owner/$Repo" --json defaultBranchRef,nameWithOwner | ConvertFrom-Json
$default = $repoInfo.defaultBranchRef.name

# === WORKFLOWS ===============================================================
Write-Host "`n>> Audit workflows..." -ForegroundColor Cyan
$wfJson = gh workflow list --all --json id,name,path,state,createdAt,updatedAt | ConvertFrom-Json
$wfRows = @()

foreach ($wf in $wfJson) {
  $last = $null
  try {
    $last = gh run list --workflow $wf.path -b $Branch -L 1 --json status,conclusion,url,createdAt | ConvertFrom-Json | Select-Object -First 1
  } catch { }

  $suggest = ""
  if ($wf.state -eq "disabled_manually") { $suggest = "ARCHIVIARE: disabilitato" }
  elseif ($wf.name -match '(?i)patched') { $suggest = "ARCHIVIARE: workflow temporaneo/patch" }
  elseif ($wf.name -match 'labeler' -and ($wfJson | Where-Object { $_.name -match 'PR Labeler' }).Count -gt 0 -and $wf.name -notmatch 'PR Labeler') {
    $suggest = "ARCHIVIARE: duplicato del PR Labeler"
  }

  $wfRows += [pscustomobject]@{
    Name       = $wf.name
    Path       = $wf.path
    State      = $wf.state
    LastStatus = $last.status
    LastResult = $last.conclusion
    LastUrl    = $last.url
    UpdatedAt  = $wf.updatedAt
    Suggestion = $suggest
  }
}

$wfCsv = Join-Path $OutDir "workflows.csv"
$wfMd  = Join-Path $OutDir "workflows.md"
$wfRows | Sort-Object Name | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $wfCsv

$md = New-Object System.Text.StringBuilder
$null = $md.AppendLine("# Workflows")
$null = $md.AppendLine()
$null = $md.AppendLine("| Name | Path | State | Last | Result | Suggestion |")
$null = $md.AppendLine("|---|---|---|---|---|---|")
foreach ($r in ($wfRows | Sort-Object Name)) {
  $last = if ($r.LastUrl) { "[link]($($r.LastUrl))" } else { "" }
  $null = $md.AppendLine("| $($r.Name) | `$($r.Path)` | $($r.State) | $last | $($r.LastResult) | $($r.Suggestion) |")
}
$md.ToString() | Set-Content -Encoding UTF8 $wfMd

# === BRANCHES REMOTI =========================================================
Write-Host "`n>> Audit branch remoti..." -ForegroundColor Cyan
git fetch --prune | Out-Null

# elenco branch remoti (origin/*) esclusi HEAD
$remoteBranches = git for-each-ref --format="%(refname:short)|%(committerdate:iso8601)" refs/remotes/origin |
  Where-Object { $_ -notmatch '->' } |
  ForEach-Object {
    $p = $_.Split('|')
    [pscustomobject]@{ Name=$p[0]; CommitDate=[datetime]$p[1] }
  } |
  Where-Object { $_.Name -ne "origin/$default" }

$cutoff = (Get-Date).AddDays(-$DaysStale)
$brRows = @()

foreach ($rb in $remoteBranches) {
  $short = $rb.Name -replace '^origin/',''
  $merged = $false
  try {
    # exitcode 0 se ancestor → merged
    git merge-base --is-ancestor "origin/$short" "origin/$default"
    $merged = $LASTEXITCODE -eq 0
  } catch { $merged = $false }
  $stale = ($rb.CommitDate -lt $cutoff)
  $candidate = ($merged -and $stale)

  $brRows += [pscustomobject]@{
    Branch     = $short
    CommitDate = $rb.CommitDate
    MergedInto = $default
    IsMerged   = $merged
    IsStale    = $stale
    Candidate  = $candidate
  }
}

$brCsv = Join-Path $OutDir "branches.csv"
$brTxt = Join-Path $OutDir "branches-to-delete.txt"
$brRows | Sort-Object CommitDate | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $brCsv
($brRows | Where-Object Candidate | Select-Object -ExpandProperty Branch) | Set-Content -Encoding UTF8 $brTxt

Write-Host "`nRisultati:"
Write-Host (" - Workflows:  {0}" -f $wfCsv)
Write-Host (" - Workflows:  {0}" -f $wfMd)
Write-Host (" - Branches:   {0}" -f $brCsv)
Write-Host (" - Candidati cancellazione: {0}" -f $brTxt)

Write-Host "`nNOTE:"
Write-Host " Per archiviare un workflow sposta il file fuori da '.github/workflows' (es: '.github/workflows-archive/')"
Write-Host " Per cancellare i branch candidati:"
Write-Host "   Get-Content `"$brTxt`" | ForEach-Object { git push origin --delete $_ }"
