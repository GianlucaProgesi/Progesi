param(
  [string]$Branch = 's1-2_varin-inputs',
  [string]$Title  = 'vars: S1-2 VarIn (Ass/MId/Dep), RHINO-only compat, dedupe + human hash',
  [string]$Body   = @'
VarIn: nuovi input (Assumption, MId, Dep opzionale). Hash e Info umani.
Compat RHINO-only: dedupe su content-hash, indici VarHash/VarStrictHash, summary con ID/NAME/VALC/BY/MID/DEP/ASS.
Rhino repo: persistenza IsAssumption.
Build: 0 warning. Tests: 107/107.
'@
)

$ErrorActionPreference = 'Stop'

# 1) repo & remote
git rev-parse --is-inside-work-tree | Out-Null
$remotes = (git remote) -split "`r?`n"
if (-not ($remotes | Where-Object { $_.Trim() -eq 'origin' })) {
  throw 'Remote "origin" non trovato. Aggiungilo con: git remote add origin YOUR_REMOTE_URL'
}

# 2) fetch e branch
git fetch --all --prune | Out-Null
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  try { git switch $Branch } catch {
    if (git branch --list $Branch) { git checkout $Branch } else { git checkout -b $Branch }
  }
}

# 3) stage + commit (solo se ci sono modifiche)
$changes = git status --porcelain
if ($changes) {
  git add -A
  git commit -m $Title -m $Body --no-verify
} else {
  Write-Host 'Nessuna modifica locale da committare'
}

# 4) push (set upstream se mancante)
$hasUpstream = $true
try { git rev-parse --abbrev-ref --symbolic-full-name '@{u}' | Out-Null } catch { $hasUpstream = $false }
if ($hasUpstream) { git push } else { git push -u origin $Branch }

Write-Host "`nDONE: push su origin/$Branch"
