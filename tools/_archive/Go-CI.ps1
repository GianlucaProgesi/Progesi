param(
  [string]$Message = "chore: run CI",
  [switch]$OpenRun,
  [switch]$RunWorkflow,                         # forza una workflow_dispatch oltre al push
  [string]$WorkflowFile = ".github/workflows/ci.yml",  # usa il FILE, non il name
  [string]$Branch,                              # default: branch corrente
  [double]$BaselineLine,
  [double]$BaselineBranch,
  [double]$BaselineMethod
)

function Get-RepoRoot {
  $p = (git rev-parse --show-toplevel) 2>$null
  if (-not $p) { throw "Non è un repo git valido." }
  return $p.Trim()
}

function Ensure-GH {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Warning "GitHub CLI (gh) non trovato. Installalo per le funzioni extra (workflow run/open)."
    return $false
  }
  return $true
}

try {
  $root = Get-RepoRoot
  Set-Location $root

  # Aggiorna baseline se richiesto
  if ($PSBoundParameters.ContainsKey('BaselineLine') -and
      $PSBoundParameters.ContainsKey('BaselineBranch') -and
      $PSBoundParameters.ContainsKey('BaselineMethod')) {

    $baselinePath = Join-Path $root "tests/coverage-baseline.json"
    $obj = @{
      line   = [math]::Round($BaselineLine,   1)
      branch = [math]::Round($BaselineBranch, 1)
      method = [math]::Round($BaselineMethod, 1)
    } | ConvertTo-Json -Compress
    $obj | Set-Content $baselinePath -NoNewline -Encoding UTF8
    Write-Host "Baseline aggiornata: $obj -> $baselinePath"
  }

  # Branch corrente
  if (-not $Branch) {
    $Branch = (git rev-parse --abbrev-ref HEAD).Trim()
  }

  # Commit & push
  git add -A
  if (-not (git diff --cached --quiet)) {
    git commit -m $Message
  } else {
    Write-Host "Nessuna modifica da commitare." -ForegroundColor Yellow
  }
  git push origin $Branch

  $ghOk = Ensure-GH

  if ($RunWorkflow -and $ghOk) {
    if (-not (Test-Path $WorkflowFile)) {
      Write-Warning "Workflow file non trovato: $WorkflowFile"
    } else {
      Write-Host "Eseguo workflow_dispatch su '$WorkflowFile'..."
      $res = gh workflow run $WorkflowFile --ref $Branch 2>&1
      if ($LASTEXITCODE -ne 0) {
        Write-Warning $res
        Write-Warning "Se vedi 422, verifica che il workflow contenga 'workflow_dispatch' e che sia valido."
      }
    }
  } else {
    Write-Host "Non lancio workflow_dispatch (usa -RunWorkflow per forzarlo)."
  }

  if ($OpenRun -and $ghOk) {
    Start-Sleep -Seconds 3
    $id = gh run list --branch $Branch -L 1 --json databaseId --jq ".[0].databaseId" 2>$null
    if ($id) {
      Write-Host "Apro il run $id..."
      gh run view $id --web
    } else {
      Write-Warning "Nessun run trovato; riprova tra pochi secondi."
    }
  }

  Write-Host "Done."
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
