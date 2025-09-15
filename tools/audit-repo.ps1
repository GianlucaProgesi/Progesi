# tools/audit-repo.ps1
<#
.SYNOPSIS
  Audit dei workflow GitHub e dei branch remoti "origin/*":
    - Elenco workflow (attivi/disabilitati)
    - Branch remoti mergiati nel default e "stale"

.DESCRIPTION
  Non modifica nulla: produce report in ./out/audit-<timestamp>/
  Crea: workflows.csv, workflows.md, branches.csv, branches-to-delete.txt (anche se vuoto)
  Richiede: Git; facoltativa la GitHub CLI `gh` (per la parte workflow)

.PARAMETER DaysStale
  Giorni per considerare un branch "stale" (default: 60)

.PARAMETER OutDir
  Cartella output. Se assente, crea ./out/audit-<timestamp> nella root del repo.

.PARAMETER DefaultBranch
  Branch di default (es. "main"). Se non indicato, lo rileva (gh -> git -> symbolic-ref).

.EXAMPLES
  pwsh -File ./tools/audit-repo.ps1
  pwsh -File ./tools/audit-repo.ps1 -DaysStale 90
  pwsh -File ./tools/audit-repo.ps1 -OutDir .\out\audit-manuale

.NOTES
  Per archiviare un workflow: sposta il file FUORI da ".github/workflows" (es.: ".github/workflows-archive/…")
  Per cancellare i branch candidati:
    Get-Content "<OutDir>\branches-to-delete.txt" | ? { $_ } | % { git push origin --delete $_ }
#>

[CmdletBinding()]
param(
  [int]$DaysStale = 60,
  [string]$OutDir,
  [string]$DefaultBranch
)

$ErrorActionPreference = 'Stop'

function Get-RepoNwo {
  $nwo = & gh repo view --json nameWithOwner -q .nameWithOwner 2>$null
  if ($nwo) { return $nwo }

  $url = git remote get-url origin 2>$null
  if (-not $url) { return $null }
  if ($url -match 'github\.com[:/](.+?)(?:\.git)?$') { return $Matches[1] }
  return $null
}

function Get-DefaultBranch {
  param([string]$Provided)
  if ($Provided) { return $Provided }

  $val = & gh repo view --json defaultBranchRef -q .defaultBranchRef.name 2>$null
  if ($val) { return $val }

  $line = git remote show origin 2>$null | Select-String 'HEAD branch'
  if ($line) { return ($line.ToString().Split(':')[-1].Trim()) }

  $sym = git symbolic-ref refs/remotes/origin/HEAD 2>$null
  if ($sym) { return ($sym -replace '^refs/remotes/origin/','') }

  throw "Impossibile determinare il branch di default (origin/HEAD)."
}

function Ensure-OutDir {
  param([string]$PathHint)
  if ($PathHint -and -not [string]::IsNullOrWhiteSpace($PathHint)) {
    New-Item -ItemType Directory -Force -Path $PathHint | Out-Null
    return (Resolve-Path $PathHint).Path
  }
  $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
  $path  = Join-Path (Get-Location) "out\audit-$stamp"
  New-Item -ItemType Directory -Force -Path $path | Out-Null
  return (Resolve-Path $path).Path
}

function Audit-Workflows {
  param([string]$OutDir)

  Write-Host "`n>> Audit workflows..."

  $wfCsv = Join-Path $OutDir 'workflows.csv'
  $wfMd  = Join-Path $OutDir 'workflows.md'

  $wfJson = @()
  if (Get-Command gh -ErrorAction SilentlyContinue) {
    try {
      # SOLO campi supportati da gh: id,name,path,state
      $wfJson = gh workflow list --all --json id,name,path,state | ConvertFrom-Json
    } catch {
      Write-Warning "Impossibile leggere i workflow via 'gh': $($_.Exception.Message). Continuerò senza workflow."
      $wfJson = @()
    }
  } else {
    Write-Warning "GitHub CLI 'gh' non trovata. Salto audit workflow."
  }

  # CSV (sempre creato, anche se vuoto)
  if ($wfJson.Count -gt 0) {
    $wfJson | Select-Object id,name,path,state |
      Sort-Object name |
      Export-Csv -Path $wfCsv -NoTypeInformation -Encoding UTF8
  } else {
    # header solo
    @([pscustomobject]@{ id=''; name=''; path=''; state='' }) |
      Select-Object id,name,path,state |
      Export-Csv -Path $wfCsv -NoTypeInformation -Encoding UTF8
    $header = Get-Content $wfCsv | Select-Object -First 1
    Set-Content -Path $wfCsv -Value $header -Encoding UTF8
  }

  # Markdown
  $sb = [System.Text.StringBuilder]::new()
  [void]$sb.AppendLine("# Workflows")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("Generato: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  ")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("| State | Path | Name |")
  [void]$sb.AppendLine("|------:|:-----|:-----|")

  if ($wfJson.Count -gt 0) {
    foreach ($w in ($wfJson | Sort-Object name)) {
      $state = $w.state
      $path  = $w.path
      $name  = $w.name
      [void]$sb.AppendLine("| $state | `$path` | $name |")
    }
  } else {
    [void]$sb.AppendLine("| (n/a) | (n/a) | (n/a) |")
  }

  Set-Content -Path $wfMd -Value $sb.ToString() -Encoding UTF8
}

function Audit-Branches {
  param(
    [string]$OutDir,
    [string]$DefaultBranch,
    [int]$DaysStale
  )

  Write-Host "`n>> Audit branch remoti..."
  git fetch --prune | Out-Null

  $cutoff = (Get-Date).AddDays(-$DaysStale)

  $refs = git for-each-ref --format="%(refname:short)|%(committerdate:iso8601)" refs/remotes/origin |
    Where-Object { $_ -match '\|' -and $_ -notmatch '->' }

  $remoteBranches = foreach ($line in $refs) {
    $parts = $line -split '\|',2
    $name  = $parts[0]
    if (-not $name) { continue }
    if ($name -eq 'origin/HEAD' -or $name -eq "origin/$DefaultBranch") { continue }
    if ($name -eq 'origin' -or $name -eq 'origin/origin') { continue }

    [pscustomobject]@{
      Name       = $name
      CommitDate = [datetime]$parts[1]
    }
  }

  $rows = @()
  foreach ($rb in $remoteBranches) {
    $short = $rb.Name -replace '^origin/',''
    if ([string]::IsNullOrWhiteSpace($short) -or $short -eq 'origin') { continue }

    git merge-base --is-ancestor "origin/$short" "origin/$DefaultBranch" *> $null
    $merged = ($LASTEXITCODE -eq 0)
    $stale  = ($rb.CommitDate -lt $cutoff)

    $rows += [pscustomobject]@{
      Branch     = $short
      CommitDate = $rb.CommitDate
      MergedInto = $DefaultBranch
      IsMerged   = $merged
      IsStale    = $stale
      Candidate  = ($merged -and $stale)
    }
  }

  $brCsv = Join-Path $OutDir 'branches.csv'
  $rows |
    Sort-Object @{Expression='Candidate';Descending=$true}, CommitDate -Descending |
    Export-Csv -Path $brCsv -NoTypeInformation -Encoding UTF8

  $candidates = $rows | Where-Object { $_.Candidate } | Select-Object -ExpandProperty Branch
  $txtPath = Join-Path $OutDir 'branches-to-delete.txt'
  $toWrite = if ($candidates) { ($candidates -join [Environment]::NewLine) } else { "" }
  Set-Content -Path $txtPath -Value $toWrite -Encoding UTF8
}

# ---------------------------
# MAIN
# ---------------------------

$repoNwo = Get-RepoNwo
if (-not $repoNwo) { $repoNwo = "(sconosciuto)" }

$currentBranch = (git branch --show-current 2>$null)
if (-not $currentBranch) { $currentBranch = "(sconosciuto)" }

$default = Get-DefaultBranch -Provided:$DefaultBranch
$OutDir  = Ensure-OutDir -PathHint:$OutDir

Write-Host ("Repo: {0} | Branch: {1} | Out: {2}" -f $repoNwo,$currentBranch,$OutDir)

Audit-Workflows -OutDir $OutDir
Audit-Branches  -OutDir $OutDir -DefaultBranch $default -DaysStale $DaysStale

Write-Host ""
Write-Host "Risultati:" -ForegroundColor Cyan
Write-Host (" - Workflows:  {0}" -f (Join-Path $OutDir 'workflows.csv'))
Write-Host (" - Workflows:  {0}" -f (Join-Path $OutDir 'workflows.md'))
Write-Host (" - Branches:   {0}" -f (Join-Path $OutDir 'branches.csv'))
Write-Host (" - Candidati cancellazione: {0}" -f (Join-Path $OutDir 'branches-to-delete.txt'))

Write-Host ""
Write-Host "NOTE:" -ForegroundColor Yellow
Write-Host " Per archiviare un workflow sposta il file fuori da '.github/workflows' (es.: '.github/workflows-archive/')."
Write-Host ' Per cancellare i branch candidati:'
Write-Host ('   Get-Content "{0}" | ForEach-Object {{ git push origin --delete $_ }}' -f (Join-Path $OutDir 'branches-to-delete.txt'))
