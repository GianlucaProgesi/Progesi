# tools/Update-Readme-ReleaseSection.ps1
[CmdletBinding()]
param(
  [string]$ReadmePath = './README.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Costruzione blocco come array (niente here-string)
$blockLines = @(
  '## 🔧 Release & Maintenance',
  '',
  '- 📖 **Release Flow:** vedi [docs/RELEASE-FLOW.md](docs/RELEASE-FLOW.md)  ',
  '- 🛠️ **Maintenance Checklist:** vedi [docs/RELEASE-MAINTENANCE.md](docs/RELEASE-MAINTENANCE.md)  ',
  '- 🚦 **Health Check (prima di un rilascio):',
  '  ```powershell',
  '  pwsh -File ./tools/Release-HealthCheck.ps1',
  '  ```',
  '- 🚀 **One-liner di rilascio:**',
  '  ```powershell',
  '  # simulazione',
  '  pwsh -File ./tools/End-to-End-Release.ps1 -DryRun',
  '',
  '  # rilascio reale',
  '  pwsh -File ./tools/End-to-End-Release.ps1',
  '  ```'
)
$block = $blockLines -join "`n"

$start = '<!-- PROGESI:RELMAINT:START -->'
$end   = '<!-- PROGESI:RELMAINT:END -->'

function Insert-Or-Replace {
  param(
    [Parameter(Mandatory=$true)][string]$Content,
    [Parameter(Mandatory=$true)][string]$StartMarker,
    [Parameter(Mandatory=$true)][string]$EndMarker,
    [Parameter(Mandatory=$true)][string]$Payload
  )
  $managed = $StartMarker + "`n`n" + $Payload.TrimEnd() + "`n`n" + $EndMarker

  if ($Content -match [regex]::Escape($StartMarker)) {
    $pattern = [regex]::Escape($StartMarker) + '(?s).*?' + [regex]::Escape($EndMarker)
    return [regex]::Replace($Content, $pattern, $managed)
  } else {
    # Inserisci dopo il titolo principale (# ...) se presente, altrimenti in fondo
    $lines = $Content -split "`r?`n"
    $titleIdx = -1
    for ($i=0; $i -lt $lines.Count; $i++) {
      if ($lines[$i] -match '^\s*#\s+') { $titleIdx = $i; break }
    }
    if ($titleIdx -ge 0 -and $titleIdx -lt ($lines.Count-1)) {
      $before = $lines[0..$titleIdx]
      $after  = $lines[($titleIdx+1)..($lines.Count-1)]
      return ($before + @('') + @($managed) + @('') + $after) -join "`n"
    } else {
      return ($Content.TrimEnd() + "`n`n" + $managed + "`n")
    }
  }
}

# Carica README (o crea base)
$content = if (Test-Path -LiteralPath $ReadmePath) {
  Get-Content -LiteralPath $ReadmePath -Raw
} else {
  "# Progesi`n`n"
}

# Applica blocco gestito
$out = Insert-Or-Replace -Content $content -StartMarker $start -EndMarker $end -Payload $block
$out = $out -replace "`r`n","`n"

# Scrivi UTF-8 senza BOM (robusto su PS5)
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReadmePath, $out, $enc)

Write-Host "README aggiornato con sezione 'Release & Maintenance'." -ForegroundColor Green
