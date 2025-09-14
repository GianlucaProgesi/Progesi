<#
.SYNOPSIS
Pulizia repo: bin/ obj/ .vs/ TestResults/ (ecc.). Mantiene MergedCoverage salvo -RemoveMergedCoverage.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
  [switch]$RemoveMergedCoverage,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
Write-Verbose "Repo root: $root"

function Remove-Dir([string]$path) {
  if (Test-Path -LiteralPath $path -PathType Container) {
    if ($PSCmdlet.ShouldProcess($path, 'Remove')) {
      Remove-Item -LiteralPath $path -Recurse -Force:$Force -ErrorAction SilentlyContinue
      Write-Verbose "Removed: $path"
    }
  }
}

# 1) bin/ e obj/ ovunque
Get-ChildItem -Path $root -Directory -Recurse -Force -Filter bin  | ForEach-Object { Remove-Dir $_.FullName }
Get-ChildItem -Path $root -Directory -Recurse -Force -Filter obj  | ForEach-Object { Remove-Dir $_.FullName }

# 2) .vs ovunque
Get-ChildItem -Path $root -Directory -Recurse -Force -Filter .vs  | ForEach-Object { Remove-Dir $_.FullName }

# 3) TestResults (escludi MergedCoverage se non richiesto)
Get-ChildItem -Path $root -Directory -Recurse -Force -Filter TestResults | ForEach-Object {
  $tr = $_.FullName
  # rimuovi sottocartelle
  Get-ChildItem -Path $tr -Directory -Force | ForEach-Object {
    if (-not $RemoveMergedCoverage -and $_.Name -ieq 'MergedCoverage') { return }
    Remove-Dir $_.FullName
  }
  # rimuovi file sciolti dentro TestResults
  Get-ChildItem -Path $tr -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
    if ($PSCmdlet.ShouldProcess($_.FullName,'Remove')) {
      Remove-Item -LiteralPath $_.FullName -Force:$Force -ErrorAction SilentlyContinue
      Write-Verbose "Removed file: $($_.FullName)"
    }
  }
}

# 4) Cartelle "rumore" in root repo
Remove-Dir (Join-Path $root 'CoverageReport')
Get-ChildItem -Path $root -Directory -Force -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -like 'diagnostics-*' -or $_.Name -like 'run-report-*' } |
  ForEach-Object { Remove-Dir $_.FullName }

Write-Verbose "Done."
