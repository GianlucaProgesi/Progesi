<#
.SYNOPSIS
Esegue SOLO i passi locali: test+coverage e hotspots.
#>
[CmdletBinding()]
param(
  [int]$MinLine = 79,
  [int]$Top = 20
)

pwsh -File "$PSScriptRoot\coverage.ps1" -RunTests -MinLine $MinLine
pwsh -File "$PSScriptRoot\coverage-hotspots.ps1" -Top $Top
