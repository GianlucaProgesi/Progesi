<# 
.SYNOPSIS
  Elenca/Elimina branch remoti stantii (dry-run di default).

.EXAMPLE
  pwsh ./tools/prune-branches.ps1 -DaysOld 90
  pwsh ./tools/prune-branches.ps1 -DaysOld 120 -Delete
#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
  [int]$DaysOld = 120,
  [string[]]$Protect = @('main','master','develop','release','chore/restore-axisvariable'),
  [switch]$Delete
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
Set-Location $root

# fetch
git fetch --prune | Out-Null

# PR aperte (se hai gh)
$keepByPR = @()
if (Get-Command gh -ErrorAction SilentlyContinue) {
  $json = gh pr list --state open --json headRefName --limit 200 | ConvertFrom-Json
  if ($json) { $keepByPR = $json.headRefName }
}

# elenco
$refs = git for-each-ref --format="%(refname:short)|%(committerdate:iso8601)" refs/remotes/origin `
        | ForEach-Object {
            $parts = $_ -split '\|',2
            [pscustomobject]@{ Name=$parts[0].Replace('origin/',''); Date=[datetime]$parts[1] }
          }

$cutoff = (Get-Date).AddDays(-$DaysOld)

$stale = $refs `
  | Where-Object {
      $_.Name -ne 'HEAD' -and
      ($Protect -notcontains $_.Name) -and
      ($keepByPR -notcontains $_.Name) -and
      $_.Date -lt $cutoff
    } `
  | Sort-Object Date

if (-not $stale) { Write-Host "Nessun branch da pulire." -ForegroundColor Green; return }

Write-Host "Branch candidati alla rimozione (prima commit < $cutoff):" -ForegroundColor Yellow
$stale | Format-Table Name,Date -AutoSize

if ($Delete) {
  foreach ($b in $stale) {
    if ($PSCmdlet.ShouldProcess("origin/$($b.Name)", 'Delete remote branch')) {
      git push origin --delete $b.Name
    }
  }
  Write-Host "Pulizia completata." -ForegroundColor Green
} else {
  Write-Host "`nDry-run: nessuna cancellazione eseguita. Aggiungi -Delete per procedere." -ForegroundColor DarkGray
}
