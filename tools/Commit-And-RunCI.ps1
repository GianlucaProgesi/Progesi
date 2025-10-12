Param(
  [string]$Repo = "GianlucaProgesi/Progesi",
  [string]$CommitMessage = "chore: sync local fixes",
  [switch]$RunCI = $true,
  [switch]$RunBuildPack = $false,
  [switch]$CreateTag = $false,
  [string]$TagPrefix = "v"
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# prerequisiti
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail "git non trovato."; exit 1 }
if (-not (Get-Command gh  -ErrorAction SilentlyContinue)) { Fail "GitHub CLI (gh) non trovato."; exit 1 }
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { Info "Login gh..."; gh auth login -w; gh auth status | Out-Null }

# default branch
$remoteInfo = git remote show origin | Out-String
$DefaultBranch = (($remoteInfo -split "`r?`n") | Where-Object { $_ -match 'HEAD branch:' }) -replace '.*HEAD branch:\s*',''
$DefaultBranch = $DefaultBranch.Trim()
if ([string]::IsNullOrWhiteSpace($DefaultBranch)) { $DefaultBranch = "main" }

# stato locale
$currentBranch = (git rev-parse --abbrev-ref HEAD | Out-String).Trim()
$changes = (git status --porcelain | Out-String).Trim()

# se sei su main, crea branch di lavoro
$branch = $currentBranch
if ($currentBranch -eq $DefaultBranch) {
  $ts = Get-Date -Format "yyyyMMdd-HHmmss"
  $branch = "chore/ci-$ts"
  Info ("Creo branch: {0}" -f $branch)
  git checkout -b $branch
}

# commit (solo se ci sono modifiche)
if (-not [string]::IsNullOrWhiteSpace($changes)) {
  Info "Committo le modifiche..."
  git add -A
  git commit -m $CommitMessage
} else {
  Warn "Nessuna modifica locale da committare."
}

# push + upstream
Info ("Push su origin {0}..." -f $branch)
git push -u origin $branch

# crea PR se non sei su main
$prUrl = ""
if ($branch -ne $DefaultBranch) {
  Info ("Creo Pull Request verso {0}..." -f $DefaultBranch)
  try {
    $prUrl = gh pr create --repo $Repo --base $DefaultBranch --head $branch --title $CommitMessage --body "Auto-PR generata dallo script Commit-And-RunCI.ps1"
    Ok ("PR creata: {0}" -f $prUrl)
  } catch {
    Warn "PR forse giÃ  esistente. Apro info PR..."
    gh pr view --repo $Repo --head $branch
  }
}

# avvia CI (se possibile)
if ($RunCI) {
  Info ("Avvio workflow CI su ref {0}..." -f $branch)
  gh workflow run CI --ref $branch 2>$null
  if ($LASTEXITCODE -eq 0) { Ok "CI avviata (workflow_dispatch)." }
  else { Warn "CI non avviata via dispatch (verrÃ  lanciata automaticamente dalla PR/push)." }
}

# avvia build-and-pack (manuale)
if ($RunBuildPack) {
  Info ("Avvio build-and-pack su ref {0}..." -f $branch)
  gh workflow run build-and-pack --ref $branch 2>$null
  if ($LASTEXITCODE -eq 0) { Ok "build-and-pack avviato (manuale)." }
  else { Warn "build-and-pack non ha workflow_dispatch o non Ã¨ su main; usa tag v* o il pulsante in Actions." }
}

# crea tag (opzionale)
if ($CreateTag) {
  $ts = Get-Date -Format "yyyy.MM.dd.HHmm"
  $tag = "{0}{1}" -f $TagPrefix, $ts
  Info ("Creo tag {0} e push..." -f $tag)
  git tag $tag
  git push origin $tag
  Ok ("Tag creato: {0}" -f $tag)
}

Ok "Fatto."
