param(
  [string]$Message = "ci: trigger",
  [string]$WorkflowFile = ".github/workflows/ci.yml", # path del workflow nel repo
  [string]$Branch,                                    # se omesso usa il branch corrente
  [switch]$OpenRun,                                   # apre il run nel browser
  [int]$MaxAttempts = 10,                             # tentativi di polling del run
  [int]$DelaySeconds = 3                              # attesa tra i tentativi
)

function Get-RepoRoot {
  $p = (git rev-parse --show-toplevel) 2>$null
  if (-not $p) { throw "Non è un repository git valido (git rev-parse fallito)." }
  return $p.Trim()
}

function Ensure-GH {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Warning "GitHub CLI (gh) non trovato. Eseguo solo push (il workflow partirà da push se configurato)."
    return $false
  }
  return $true
}

try {
  $root = Get-RepoRoot
  Set-Location $root

  if (-not $Branch) {
    $Branch = (git rev-parse --abbrev-ref HEAD).Trim()
  }

  # Stage + commit se ci sono modifiche
  git add -A
  if (-not (git diff --cached --quiet)) {
    git commit -m $Message
  } else {
    Write-Host "Nessuna modifica da commitare." -ForegroundColor Yellow
  }

  # Push
  git push origin $Branch

  # Dispatch del workflow
  $ghOk = Ensure-GH
  if ($ghOk) {
    if (-not (Test-Path $WorkflowFile)) {
      Write-Warning "Workflow file non trovato: $WorkflowFile"
    } else {
      Write-Host "Eseguo workflow_dispatch su '$WorkflowFile' (branch: $Branch)..."
      $res = gh workflow run $WorkflowFile --ref $Branch 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Warning $res
        Write-Warning "Dispatch fallito (422 se manca 'workflow_dispatch' o YAML non valido)."
      } else {
        Write-Host "Dispatch inviato."
        if ($OpenRun) {
          # Poll dell'ultimo run del branch e apertura in browser
          $runId = $null
          for ($i = 0; $i -lt $MaxAttempts; $i++) {
            $runId = gh run list --branch $Branch -L 1 --json databaseId --jq ".[0].databaseId" 2>$null
            if ($runId) { break }
            Start-Sleep -Seconds $DelaySeconds
          }
          if ($runId) {
            Write-Host "Apro il run $runId..."
            gh run view $runId --web
          } else {
            Write-Warning "Impossibile individuare il run appena creato; aprilo da GitHub → Actions."
          }
        }
      }
    }
  }

  Write-Host "Done."
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
