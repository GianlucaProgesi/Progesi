<#  Clean build artifacts (bin/obj/.vs/TestResults*) in the repo, safely.

    Usage:
      # Dry-run (non rimuove nulla, mostra cosa farebbe)
      pwsh ./tools/clean-solution.ps1 -WhatIf -Verbose

      # Applica realmente
      pwsh ./tools/clean-solution.ps1 -Verbose

      # Applica realmente forzando la rimozione di file bloccati
      pwsh ./tools/clean-solution.ps1 -Verbose -Force

      # Rimuove anche TestResults/MergedCoverage
      pwsh ./tools/clean-solution.ps1 -RemoveMergedCoverage
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
  [switch]$RemoveMergedCoverage,
  [switch]$IncludeNodeModules,
  [switch]$IncludePackages,
  [string[]]$ExtraPatterns,
  [switch]$Force
)

function Get-RepoRoot {
  # Risali finché non trovi .git (root del repo).
  $dir = $PSScriptRoot
  while ($null -ne $dir -and -not (Test-Path (Join-Path $dir '.git'))) {
    $dir = Split-Path -Parent $dir
  }
  if (-not $dir) {
    # fallback: parent di tools
    $dir = Split-Path -Parent $PSScriptRoot
  }
  return $dir
}

$root = Get-RepoRoot
Write-Verbose "Repo root: $root"

# ---- raccogli i target da eliminare -----------------------------------------
$targets = New-Object System.Collections.Generic.List[System.IO.FileSystemInfo]

# bin / obj in tutto il repo
Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @('bin','obj') } |
  ForEach-Object { $targets.Add($_) }

# .vs alla root (ed eventuali sottocartelle .vs generate da IDE)
Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -eq '.vs' } |
  ForEach-Object { $targets.Add($_) }

# TestResults → mantieni MergedCoverage per default
$testResultsRoots = Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
                    Where-Object { $_.Name -eq 'TestResults' }

foreach ($tr in $testResultsRoots) {
  $merged = Join-Path $tr.FullName 'MergedCoverage'
  if ($RemoveMergedCoverage) {
    $targets.Add($tr)
  }
  else {
    if (Test-Path $tr.FullName) {
      # aggiungi tutto ciò che NON è MergedCoverage
      Get-ChildItem -Force -LiteralPath $tr.FullName |
        Where-Object { -not ($_.PSIsContainer -and $_.Name -eq 'MergedCoverage') } |
        ForEach-Object { $targets.Add($_) }
    }
  }
}

# opzionali
if ($IncludeNodeModules) {
  Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'node_modules' } |
    ForEach-Object { $targets.Add($_) }
}
if ($IncludePackages) {
  Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'packages' } |
    ForEach-Object { $targets.Add($_) }
}

# Extra glob locali (es.: '**/*.user', '**/*.suo')
if ($ExtraPatterns) {
  foreach ($pat in $ExtraPatterns) {
    # Per far funzionare -Include servono wildcard nel Path
    Get-ChildItem -Path (Join-Path $root '*') -Recurse -Force -ErrorAction SilentlyContinue -Include $pat |
      ForEach-Object { $targets.Add($_) }
  }
}

# de-duplica
$targets = $targets | Sort-Object FullName -Unique

# ---- rimozione con ShouldProcess (supporta -WhatIf / -Confirm) --------------
$removed = 0
foreach ($t in $targets) {
  if ($PSCmdlet.ShouldProcess($t.FullName, "Remove")) {
    try {
      Remove-Item -LiteralPath $t.FullName -Recurse -Force:$Force -ErrorAction Stop
      Write-Verbose "Removed: $($t.FullName)"
      $removed++
    }
    catch {
      Write-Warning "Skip: $($t.FullName) → $($_.Exception.Message)"
    }
  }
  else {
    Write-Verbose "Would remove: $($t.FullName)"
  }
}

Write-Host ("Pulizia completata. {0} elementi {1}." -f
  $removed,
  ($WhatIfPreference -or $PSCmdlet.ShouldProcess('x','y','z')) ? 'segnalati' : 'rimossi'
)
