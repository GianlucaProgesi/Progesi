Param(
  [Parameter(Mandatory=$true)][int]$PRNumber,
  [string]$Repo = "GianlucaProgesi/Progesi",
  [switch]$DeleteBranch = $true
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# 0) gh presente + login
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Fail "GitHub CLI (gh) non trovato. Installa https://cli.github.com/"
  exit 1
}
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { gh auth login -w; gh auth status | Out-Null }

# 1) Stato PR
$raw = gh pr view $PRNumber --repo $Repo --json number,state,mergeStateStatus,reviewDecision,headRefName,baseRefName 2>$null | Out-String
if ([string]::IsNullOrWhiteSpace($raw)) { Fail "PR #$PRNumber non trovata."; exit 1 }
$pr = $raw | ConvertFrom-Json

Ok ("PR #{0} → state={1}, mergeState={2}, review={3}, head={4}, base={5}" -f `
  $pr.number, $pr.state, $pr.mergeStateStatus, $pr.reviewDecision, $pr.headRefName, $pr.baseRefName)

if ($pr.state -ne "OPEN") { Fail "La PR non è OPEN."; exit 1 }

# 2) Merge (squash)
$flags = @("--repo", $Repo, $PRNumber.ToString(), "--squash", "-y")
if ($DeleteBranch.IsPresent) { $flags += "--delete-branch" }

Info ("Eseguo: gh pr merge #{0} --squash {1}" -f $PRNumber, (if ($DeleteBranch) {"--delete-branch"} else {""}))
$out = gh pr merge @flags 2>&1
$out | ForEach-Object { $_ | Write-Host }

if ($LASTEXITCODE -eq 0) { Ok "Squash merge completato."; exit 0 }

Fail "Squash merge fallito."
if ($out -match "Required status check") {
  Write-Host "→ La regola del branch richiede status checks: imposta i required corretti o disattivali temporaneamente." -ForegroundColor Yellow
} elseif ($out -match "not mergeable" -or $out -match "conflict") {
  Write-Host "→ La PR ha conflitti o è BLOCKED (review/checks)." -ForegroundColor Yellow
} elseif ($out -match "Merge commits are not allowed") {
  Write-Host "→ Hai 'Require linear history': usa --squash (già in uso) o --rebase, non --merge." -ForegroundColor Yellow
}
exit 1
