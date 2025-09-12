<#
.SYNOPSIS
  Aggiunge tutto, committa, pusha, (opzionale) crea/usa branch, tagga e (opzionale) apre una PR.

.EXAMPLES
  pwsh -File tools/Push-Commit-And-Tag.ps1 -Message "ci: fix windows x64 tests"
  pwsh -File tools/Push-Commit-And-Tag.ps1 -Message "feat: X" -NewBranch -Branch "chore/ci-fix-windows-x64"
  pwsh -File tools/Push-Commit-And-Tag.ps1 -Message "release: v0.1.0" -Tag "v0.1.0" -TagMessage "First beta" -PushTags
  pwsh -File tools/Push-Commit-And-Tag.ps1 -Message "chore: update" -OpenPR -PRBase "main"

.NOTES
  Richiede git e (per -OpenPR) GitHub CLI `gh` autenticato.
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$Message,

  # Nome branch: se -NewBranch è impostato, verrà creato; se vuoto, usa il branch corrente.
  [string]$Branch = "",

  # Crea e switcha a un nuovo branch (se $Branch è indicato).
  [switch]$NewBranch,

  # Crea un tag; se omesso, nessun tag.
  [string]$Tag,

  # Messaggio per tag annotato; se omesso, crea un tag leggero.
  [string]$TagMessage = "",

  # Pusha anche i tag (consigliato quando si crea un tag).
  [switch]$PushTags,

  # Apre una Pull Request via GitHub CLI.
  [switch]$OpenPR,

  # Base branch per la PR (default: main).
  [string]$PRBase = "main"
)

# --- Utility ---
function Invoke-OrThrow {
  param([string]$Cmd)
  Write-Host ">> $Cmd" -ForegroundColor Cyan
  $global:LASTEXITCODE = 0
  & $env:COMSPEC /c $Cmd
  if (${LASTEXITCODE} -ne 0) {
    throw "Command failed with exit code ${LASTEXITCODE}: $Cmd"
  }
}

try {
  # Verifiche di base
  if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git non è disponibile nel PATH."
  }
  if ($OpenPR -and -not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) non è disponibile nel PATH ma è richiesta da -OpenPR."
  }

  # Vai alla root del repo
  $repoRoot = (& git rev-parse --show-toplevel).Trim()
  if (-not $repoRoot) { throw "Non sembra una repo Git." }
  Set-Location $repoRoot

  # Stato iniziale
  Write-Host "Repository: $repoRoot" -ForegroundColor Yellow
  $currentBranch = (& git rev-parse --abbrev-ref HEAD).Trim()
  Write-Host "Branch corrente: $currentBranch" -ForegroundColor DarkGray

  # Gestione branch
  if ($NewBranch) {
    if (-not $Branch) { throw "-NewBranch richiede -Branch <nome>" }
    Write-Host "Creo e switcho a nuovo branch: $Branch" -ForegroundColor Yellow
    Invoke-OrThrow "git checkout -b `"$Branch`""
  } elseif ($Branch) {
    Write-Host "Switcho al branch esistente: $Branch" -ForegroundColor Yellow
    Invoke-OrThrow "git checkout `"$Branch`""
  } else {
    Write-Host "Uso il branch corrente: $currentBranch" -ForegroundColor Yellow
  }

  # Add + commit (solo se ci sono cambi)
  Invoke-OrThrow "git add -A"
  $status = (& git status --porcelain)
  if (-not $status) {
    Write-Host "Nessuna modifica da committare." -ForegroundColor DarkYellow
  } else {
    Invoke-OrThrow "git commit -m `"$Message`""
  }

  # Push ramo corrente (dopo eventuale creation/switch)
  $activeBranch = (& git rev-parse --abbrev-ref HEAD).Trim()
  Write-Host "Pusho su origin/$activeBranch" -ForegroundColor Yellow
  Invoke-OrThrow "git push -u origin `"$activeBranch`""

  # Tag opzionale
  if ($Tag) {
    if ($TagMessage) {
      Invoke-OrThrow "git tag -a `"$Tag`" -m `"$TagMessage`""
    } else {
      Invoke-OrThrow "git tag `"$Tag`""
    }

    if ($PushTags) {
      Invoke-OrThrow "git push --tags"
    } else {
      Write-Host "Tag creato localmente (usa -PushTags per pubblicarlo)." -ForegroundColor DarkYellow
    }
  }

  # PR opzionale
  if ($OpenPR) {
    # controlla auth (non strettamente necessario, ma aiuta con messaggi chiari)
    $authOk = $true
    try { & gh auth status | Out-Null } catch { $authOk = $false }
    if (-not $authOk) {
      Write-Warning "gh non risulta autenticato (gh auth login). Provo comunque a creare la PR…"
    }

    Write-Host "Apro Pull Request verso $PRBase..." -ForegroundColor Yellow
    # --fill usa titolo/descrizione dal template/commit; se vuoi solo il titolo, puoi rimuovere --fill
    Invoke-OrThrow "gh pr create --base `"$PRBase`" --head `"$activeBranch`" --title `"$Message`" --fill"
  }

  Write-Host "Operazione completata." -ForegroundColor Green
}
catch {
  Write-Error $_.Exception.Message
  exit 1
}
