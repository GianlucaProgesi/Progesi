[CmdletBinding()]
param(
  [string]$Remote = 'origin',
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Ultimo tag v*
$lastTag = (git tag --list 'v*' --sort=-v:refname | Select-Object -First 1)
if (-not $lastTag) { $lastTag = 'v0.0.0' }

# Commits da lastTag..HEAD
$range = if ($lastTag -eq 'v0.0.0') { '' } else { "$lastTag..HEAD" }
$log = git log --no-merges --pretty=format:'%s%n%b%n----' $range

if ([string]::IsNullOrWhiteSpace($log)) {
  Write-Host "Nessun commit da $lastTag a HEAD. Annullato." -ForegroundColor Yellow
  exit 0
}

# Determina bump
$major = $false; $minor = $false
$lines = $log -split "`n"
foreach ($ln in $lines) {
  if ($ln -match 'BREAKING CHANGE' -or $ln -match '^[a-z]+(\(.+\))?!:') { $major = $true; break }
  if ($ln -match '^feat(\(.+\))?:') { $minor = $true }
}
$bump = if ($major) { 'major' } elseif ($minor) { 'minor' } else { 'patch' }

# Parse versione corrente
if ($lastTag -notmatch '^v(\d+)\.(\d+)\.(\d+)$') { throw "Ultimo tag non parsabile: $lastTag" }
$M = [int]$Matches[1]; $m = [int]$Matches[2]; $p = [int]$Matches[3]

switch ($bump) {
  'major' { $M++; $m=0; $p=0 }
  'minor' { $m++; $p=0 }
  'patch' { $p++ }
}
$newTag = "v$M.$m.$p"

Write-Host "Ultimo tag: $lastTag" -ForegroundColor Cyan
Write-Host "Bump: $bump  ->  Nuovo tag: $newTag" -ForegroundColor Green

if ($DryRun) { exit 0 }

# Crea/aggiorna tag annotato e push
if (git tag --list $newTag) { git tag -d $newTag | Out-Null }
git tag -a $newTag -m $newTag
git push -f $Remote $newTag
Write-Host "✓ Tag $newTag pushato su $Remote" -ForegroundColor Green
