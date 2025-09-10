<# 
.SYNOPSIS
  Commit + push + crea tag vX.Y.Z per innescare la release.

.USAGE
  pwsh ./tools/Release-NewVersion.ps1 -Version 1.0.28 [-Remote origin]
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$Version,
  [string]$Remote = "origin",
  [string]$CommitMessage = $( "chore(release): prepare v$Version (metadata + workflow)" )
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Verifiche git di base
git rev-parse --is-inside-work-tree *> $null

# Aggiungi tutto (inclusi file nuovi)
git add -A

# Se non ci sono cambi, non fare commit
$changes = git status --porcelain
if (-not [string]::IsNullOrWhiteSpace($changes)) {
  git commit -m $CommitMessage
  Write-Host "✓ Commit creato"
} else {
  Write-Host "Nessuna modifica da committare (skip commit)"
}

# Push branch corrente
$currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
git push $Remote $currentBranch
Write-Host "✓ Push branch $currentBranch"

# Crea/aggiorna tag (annotated); se esiste, lo rimpiazza
$tag = "v$Version"
$existing = git tag --list $tag
if ($existing) {
  Write-Host "Tag $tag esiste già: lo sovrascrivo"
  git tag -d $tag | Out-Null
}
git tag -a $tag -m $tag
git push -f $Remote $tag
Write-Host "✓ Tag $tag pushato (forzato se già esisteva)"

Write-Host "`nFatto. La pipeline di release dovrebbe partire sul tag $tag." -ForegroundColor Green
