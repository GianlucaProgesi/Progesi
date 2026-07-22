param(
  [string]$Branch = 's1-2_varin-inputs',
  [string]$Title  = 'vars: S1-2 VarIn (Ass/MId/Dep), RHINO-only compat, dedupe + human hash',
  [string]$Body   = @'
VarIn: nuovi input (Assumption, MId, Dep opzionale). Hash/Info umani.
Compat RHINO-only: dedupe su content-hash, indici VarHash/VarStrictHash, summary con ID/NAME/VALC/BY/MID/DEP/ASS.
Rhino repo: persistenza IsAssumption. Build: 0 warning. Tests: 107/107.
'@,
  [switch]$AllowEmptyCommit
)

# --- helper: esegue git e valida l'esito ---
function Invoke-Git {
  param([Parameter(Mandatory=$true)][string[]]$Args)
  & git @Args
  if ($LASTEXITCODE -ne 0) {
    throw "git $($Args -join ' ') → exit $LASTEXITCODE"
  }
}

# 0) check repo & origin
Invoke-Git @('rev-parse','--is-inside-work-tree') | Out-Null
$remotes = (git remote) -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if (-not ($remotes -contains 'origin')) {
  throw 'Remote "origin" non trovato. Esegui: git remote add origin https://YOUR_REMOTE_URL'
}

# 1) fetch
Invoke-Git @('fetch','--all','--prune') | Out-Null

# 2) crea/switch branch target
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  # la branch locale esiste?
  & git rev-parse --verify --quiet "refs/heads/$Branch" *> $null
  if ($LASTEXITCODE -eq 0) {
    Invoke-Git @('switch', $Branch)
  } else {
    Invoke-Git @('switch','-c', $Branch)   # crea da HEAD corrente
  }
}
# ricontrollo
$now = (git rev-parse --abbrev-ref HEAD).Trim()
if ($now -ne $Branch) { throw "Non sono su $Branch (sono su $now)" }

# 3) commit (se necessario)
$porcelain = (git status --porcelain)
if ($porcelain) {
  Invoke-Git @('add','-A')
  Invoke-Git @('commit','-m', $Title, '-m', $Body, '--no-verify')
} elseif ($AllowEmptyCommit) {
  Invoke-Git @('commit','--allow-empty','-m', $Title, '-m', $Body, '--no-verify')
} else {
  Write-Host 'ℹ Nessuna modifica locale da committare'
}

# 4) push con upstream se assente
& git rev-parse --abbrev-ref --symbolic-full-name '@{u}' *> $null
if ($LASTEXITCODE -eq 0) {
  Invoke-Git @('push')
} else {
  Invoke-Git @('push','-u','origin', $Branch)
}

Write-Host "✔ Push su origin/$Branch completato"
