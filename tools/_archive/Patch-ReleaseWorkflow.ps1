[CmdletBinding()]
param(
  [string]$WorkflowPath = ".github/workflows/release.yml"
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if (-not (Test-Path $WorkflowPath)) { throw "File workflow non trovato: $WorkflowPath" }

$orig = Get-Content -LiteralPath $WorkflowPath -Raw
$backup = "$WorkflowPath.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Set-Content -LiteralPath $backup -Value $orig -Encoding ASCII
Write-Host "Backup creato: $backup"

$lines = Get-Content -LiteralPath $WorkflowPath

# 1) Aggiungi --skip-duplicate a ogni riga con 'dotnet nuget push' se non presente
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match 'dotnet\s+nuget\s+push' -and $lines[$i] -notmatch '--skip-duplicate') {
    $lines[$i] = $lines[$i] + " --skip-duplicate"
  }
}

# 2) Assicura generate_release_notes: true nello step softprops/action-gh-release@v2
for ($i = 0; $i -lt $lines.Count; $i++) {
  if ($lines[$i] -match '^\s*-\s*uses:\s*softprops/action-gh-release@v2\s*$') {
    $j = $i + 1
    $withIndex = -1
    $genExists = $false
    while ($j -lt $lines.Count -and ($lines[$j] -notmatch '^\s*-\s*uses:')) {
      if ($lines[$j] -match '^\s*with:\s*$') { $withIndex = $j }
      if ($lines[$j] -match '^\s*generate_release_notes:\s*') { $genExists = $true }
      $j++
    }
    if ($withIndex -eq -1) {
      # Inserisci "with:" e "generate_release_notes: true" subito dopo la riga uses
      $before = $lines[0..$i]
      $after  = @()
      if (($i + 1) -le ($lines.Count - 1)) { $after = $lines[($i+1)..($lines.Count-1)] }
      $insert = @("  with:", "    generate_release_notes: true")
      $lines = $before + $insert + $after
      $i = $i + $insert.Count
    } elseif (-not $genExists) {
      # Inserisci generate_release_notes dopo 'with:'
      $before = $lines[0..$withIndex]
      $after  = @()
      if (($withIndex + 1) -le ($lines.Count - 1)) { $after = $lines[($withIndex+1)..($lines.Count-1)] }
      $lines = $before + @("      generate_release_notes: true") + $after
      $i = $withIndex + 1
    }
  }
}

$new = ($lines -join [Environment]::NewLine)
if ($new -ne $orig) {
  Set-Content -LiteralPath $WorkflowPath -Value $new -Encoding ASCII
  Write-Host "Workflow patchato con successo." -ForegroundColor Green
} else {
  Write-Host "Nessuna modifica necessaria (gia' ok)." -ForegroundColor Yellow
}
