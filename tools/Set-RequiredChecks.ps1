Param(
  [string]$Repo = "GianlucaProgesi/Progesi",         # owner/name
  [string]$Branch = "main",
  [string[]]$Contexts = @("CI / test (pull_request)"),
  [bool]$Strict = $false,                            # Require branches to be up to date
  [bool]$LinearHistory = $true                       # Require linear history
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# 0) gh presente + login
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
  Fail "GitHub CLI (gh) non trovato. Installa https://cli.github.com/ e riprova."
  exit 1
}
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) {
  Info "Login gh..."
  gh auth login -w
  gh auth status | Out-Null
  if ($LASTEXITCODE -ne 0) { Fail "Autenticazione gh fallita."; exit 1 }
}

# 1) Leggi protezione attuale
Info ("Leggo protezione corrente: repo={0} branch={1}" -f $Repo, $Branch)
$currentRaw = ""
try {
  $currentRaw = gh api "repos/$Repo/branches/$Branch/protection" -H "Accept: application/vnd.github+json" | Out-String
} catch {
  Fail "Impossibile leggere la protezione del branch. Verifica che il branch sia protetto e che l'utente abbia permessi admin."
  throw
}
if ([string]::IsNullOrWhiteSpace($currentRaw)) {
  Fail "Protezione non trovata o vuota su $Repo/$Branch."
  exit 1
}
$current = $currentRaw | ConvertFrom-Json

# 2) Prepara il corpo per l'UPDATE mantenendo il resto della config
#    - required_status_checks: strict + contexts (quelli richiesti)
#    - required_linear_history: come da parametro
#    - riproponiamo le sezioni esistenti (review/restrictions/enforce_admins) così non le perdi
$enforceAdmins = $false
try { if ($current.enforce_admins -and $current.enforce_admins.enabled) { $enforceAdmins = $true } } catch {}

$body = [ordered]@{
  required_status_checks = @{
    strict   = $Strict
    contexts = $Contexts
  }
  enforce_admins = $enforceAdmins
  required_pull_request_reviews = $current.required_pull_request_reviews
  restrictions = $current.restrictions
  required_linear_history = $LinearHistory
  # Lasciamo intatti (o null) gli altri campi opzionali
  lock_branch = $false
  allow_deletions = $false
  block_creations = $false
  allow_force_pushes = if ($current.allow_force_pushes) { $current.allow_force_pushes.enabled } else { $false }
  allow_fork_syncing = if ($current.allow_fork_syncing) { $current.allow_fork_syncing.enabled } else { $false }
  required_conversation_resolution = if ($current.required_conversation_resolution) { $current.required_conversation_resolution.enabled } else { $false }
}

# 3) PUT della protezione aggiornata (tutto in un colpo)
$tmp = New-TemporaryFile
$body | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 -Path $tmp
Info "Aggiorno branch protection (required checks + linear history)..."
gh api --method PUT "repos/$Repo/branches/$Branch/protection" `
  -H "Accept: application/vnd.github+json" `
  --input $tmp | Out-Null
Remove-Item $tmp -Force

# 4) Rileggi e mostra riepilogo
$final = gh api "repos/$Repo/branches/$Branch/protection/required_status_checks" -H "Accept: application/vnd.github+json" | Out-String
if (-not [string]::IsNullOrWhiteSpace($final)) {
  $obj = $final | ConvertFrom-Json
  Ok "Required status checks aggiornati:"
  Write-Host ("  strict (up-to-date): {0}" -f $obj.strict)
  Write-Host ("  contexts: {0}" -f (($obj.contexts -join "; ")))
} else {
  Warn "Non sono riuscito a rileggere i required checks. Apri la pagina Branch protection per verificare."
}

# 5) Conferma linear history
try {
  $lh = gh api "repos/$Repo/branches/$Branch/protection/required_linear_history" -H "Accept: application/vnd.github+json" | Out-String
  $lhObj = $lh | ConvertFrom-Json
  Ok ("Required linear history: {0}" -f ($lhObj.enabled))
} catch {
  Warn "Impossibile leggere lo stato di linear history (verifica da UI)."
}
Ok "Fatto. Ricarica Settings → Branches → Regola su '$Branch' e verifica i contesti."
