param(
  [string]$Branch = 's1-1_core-assumption',
  [string]$Title  = 'core: IsAssumption in ProgesiVariable (+hash & equality) + 0 warnings',
  [string]$Body   = @'
GH (RHINO-only): compat adapters senza SQLite; escluso SqliteVariableRepositorySmokeComponent; RhinoCommon 8.23.
Core: nuova proprietà IsAssumption; aggiornata Compute(ProgesiVariable) in ProgesiHash; test aggiunti.
Build: 0 warning; Tests: 107/107.
'@
)

$ErrorActionPreference = 'Stop'

# 1) Repo check
git rev-parse --is-inside-work-tree | Out-Null

# 2) Remote 'origin' check
$remotes = (git remote) -split "`r?`n"
if (-not ($remotes | ForEach-Object { $_.Trim() } | Where-Object { $_ -eq 'origin' })) {
  throw 'Remote "origin" non trovato. Aggiungilo con: git remote add origin YOUR_REMOTE_URL'
}

# 3) Fetch
git fetch --all --prune | Out-Null

# 4) Switch/crea branch
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  try { git switch $Branch }
  catch {
    # fallback per git vecchi
    if (git branch --list $Branch) { git checkout $Branch } else { git checkout -b $Branch }
  }
}

# 5) Staging + commit se ci sono cambi
$changes = git status --porcelain
if ($changes) {
  git add -A
  git commit -m $Title -m $Body --no-verify
}

# 6) Push (set upstream se manca)
$hasUpstream = $true
try { git rev-parse --abbrev-ref --symbolic-full-name '@{u}' | Out-Null } catch { $hasUpstream = $false }
if ($hasUpstream) { git push } else { git push -u origin $Branch }

Write-Host "DONE: push su origin/$Branch"
