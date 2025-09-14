<# 
.SYNOPSIS
  Crea un tag/branch di snapshot e li pusha su origin.

.EXAMPLE
  pwsh ./tools/repo-snapshot.ps1 -Tag "milestone/coverage-79" -Message "Snapshot dopo gate 79%"

.EXAMPLE
  pwsh ./tools/repo-snapshot.ps1 -Tag "milestone/2025-09-14" -CreateBackupBranch -Push
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$Tag,
  [string]$Message = $Tag,
  [switch]$CreateBackupBranch,
  [switch]$Push
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSCommandPath
Set-Location $root

function Invoke-Git([string[]]$args) {
  $p = Start-Process -FilePath git -ArgumentList $args -NoNewWindow -PassThru -Wait
  if ($p.ExitCode -ne 0) { throw "git $args failed (exit $($p.ExitCode))" }
}

Invoke-Git @('tag','-a',$Tag,'-m', $Message)

if ($CreateBackupBranch) {
  $branch = "backup/$($Tag.Replace('milestone/',''))"
  Invoke-Git @('branch',$branch)
  if ($Push) { Invoke-Git @('push','-u','origin',$branch) }
}

if ($Push) { Invoke-Git @('push','origin',$Tag) }

Write-Host "Snapshot creato: tag '$Tag'$(if($CreateBackupBranch){", branch '$branch'"})" -ForegroundColor Green
