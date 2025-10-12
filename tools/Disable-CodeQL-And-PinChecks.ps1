Param(
  [string]$Repo   = "GianlucaProgesi/Progesi",
  [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail "gh non trovato (https://cli.github.com/)"; exit 1 }
gh auth status 1>$null 2>$null; if ($LASTEXITCODE -ne 0) { gh auth login -w | Out-Null }

# A) DISABLE CodeQL default setup (idempotente)
Info "Disabilito Code Scanning default setup (CodeQL)..."
try {
  gh api -X DELETE "repos/$Repo/code-scanning/default-setup" -H "Accept: application/vnd.github+json" | Out-Null
  Ok "CodeQL default setup disabilitato (o non era attivo)."
} catch {
  Warn "Impossibile disabilitare CodeQL via API (forse già off). Proseguo."
}

# B) BRANCH PROTECTION: imposta i required status checks = solo 'CI / test (pull_request)'
#    1) PATCH strict=false (no 'up-to-date' obbligatorio) – modifica se lo vuoi true
Info "Imposto strict=false sui required status checks..."
try {
  gh api -X PATCH "repos/$Repo/branches/$Branch/protection/required_status_checks" `
    -H "Accept: application/vnd.github+json" -f strict=false | Out-Null
  Ok "Strict=false impostato."
} catch { Warn "PATCH strict fallita (verifica che la branch protection sia attiva). Proseguo." }

#    2) svuota contexts (se esistono)
try {
  $cur = gh api "repos/$Repo/branches/$Branch/protection/required_status_checks" -H "Accept: application/vnd.github+json" | Out-String
  if (-not [string]::IsNullOrWhiteSpace($cur)) {
    $obj = $cur | ConvertFrom-Json
    if ($obj.contexts -and $obj.contexts.Count -gt 0) {
      foreach ($c in $obj.contexts) {
        gh api -X DELETE "repos/$Repo/branches/$Branch/protection/required_status_checks/contexts" `
          -H "Accept: application/vnd.github+json" -F "contexts[]=$c" | Out-Null
      }
      Ok "Vecchi contexts rimossi."
    }
  }
} catch { Warn "Rimozione contexts non riuscita (ok se erano vuoti)." }

#    3) aggiungi SOLO 'CI / test (pull_request)'
Info "Aggiungo required: 'CI / test (pull_request)'..."
try {
  gh api -X POST "repos/$Repo/branches/$Branch/protection/required_status_checks/contexts" `
    -H "Accept: application/vnd.github+json" `
    -F "contexts[]=CI / test (pull_request)" | Out-Null
  Ok "Required checks aggiornati."
} catch {
  Fail "Impossibile aggiungere il contesto required via API. Verifica i permessi admin o la protezione branch."
  exit 1
}

Ok "Operazione completata."
