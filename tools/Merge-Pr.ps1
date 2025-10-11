Param(
  [Parameter(Mandatory=$true)][int]$PRNumber,                 # es: 47
  [string]$Repo = "GianlucaProgesi/Progesi",
  [ValidateSet("merge","squash","rebase")][string]$Method = "merge",
  [switch]$DeleteBranch = $true,
  [switch]$AutoApprove,
  [switch]$ReadyIfDraft
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# gh + login
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Fail "GitHub CLI (gh) non trovato. Installa https://cli.github.com/"
  exit 1
}
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { Info "Login gh..."; gh auth login -w; gh auth status | Out-Null }

Ok "Autenticazione gh ok."

function Get-PrObject {
  $raw = gh pr view $PRNumber --repo $Repo --json `
    number,state,isDraft,mergeStateStatus,reviewDecision,headRefName,baseRefName,maintainerCanModify,author 2>$null | Out-String
  if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
  return ($raw | ConvertFrom-Json)
}

Info ("Lettura stato PR #{0} su {1}..." -f $PRNumber, $Repo)
$prObj = Get-PrObject
if (-not $prObj) { Fail ("PR #{0} non trovata su {1}." -f $PRNumber,$Repo); exit 1 }

Ok ("PR #{0} → state={1}, draft={2}, mergeState={3}, review={4}, head={5}, base={6}" -f `
  $prObj.number, $prObj.state, $prObj.isDraft, $prObj.mergeStateStatus, $prObj.reviewDecision, $prObj.headRefName, $prObj.baseRefName)

if ($prObj.state -ne "OPEN") { Fail ("La PR non è OPEN (state={0})." -f $prObj.state); exit 1 }

if ($ReadyIfDraft -and $prObj.isDraft) {
  Info "PR in draft → gh pr ready..."
  gh pr ready $PRNumber --repo $Repo 2>$null
  $prObj = Get-PrObject
}

if ($AutoApprove -and $prObj.reviewDecision -eq "REVIEW_REQUIRED") {
  Info "Approvo PR (gh pr review --approve)..."
  gh pr review $PRNumber --repo $Repo --approve 2>$null
  $prObj = Get-PrObject
  $rd = $prObj.reviewDecision; if ([string]::IsNullOrEmpty($rd)) { $rd = "unknown" }
  Ok ("Review decision: {0}" -f $rd)
}

if ($prObj.mergeStateStatus -eq "BLOCKED") {
  Info "Dettaglio check:"
  gh pr checks $PRNumber --repo $Repo
  Write-Host ""
  Fail "PR BLOCKED (status checks o review). Se vedi 'Expected' su check rimossi (es. CodeQL), aggiorna le regole del branch protetto."
}

# Merge
$flags = @("--repo", $Repo, $PRNumber.ToString(), ("--" + $Method), "-y")
if ($DeleteBranch.IsPresent) { $flags += "--delete-branch" }

Info ("Eseguo: gh pr merge #{0} --{1} {2}" -f $PRNumber, $Method, (if ($DeleteBranch) {"--delete-branch"} else {""}))
$mergeOut = gh pr merge @flags 2>&1
$code = $LASTEXITCODE
$mergeOut | ForEach-Object { $_ | Write-Host }
if ($code -eq 0) { Ok "Merge completato."; exit 0 }

Fail "Merge fallito."
if ($mergeOut -match "Required status check") {
  Write-Host "→ Branch protetto: ci sono 'Required status checks' ancora attivi/mancanti." -ForegroundColor Yellow
} elseif ($mergeOut -match "not mergeable" -or $mergeOut -match "conflict") {
  Write-Host "→ PR con conflitti o stato non idoneo (draft/blocked)." -ForegroundColor Yellow
}
exit 1
