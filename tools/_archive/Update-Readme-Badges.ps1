# tools/Update-Readme-Badges.ps1
[CmdletBinding()]
param(
  [string]$ReadmePath = './README.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Badge markup (array -> join per evitare here-string)
$badgeLines = @(
  '[![Release Health](https://img.shields.io/badge/Release%20Health-Run%20check-2ea44f?logo=powershell&logoColor=white)](tools/Release-HealthCheck.ps1)'
)
$badge = $badgeLines -join "`n"

$start = '<!-- PROGESI:BADGES:START -->'
$end   = '<!-- PROGESI:BADGES:END -->'

function Insert-Or-Replace {
  param(
    [Parameter(Mandatory=$true)][string]$Content,
    [Parameter(Mandatory=$true)][string]$StartMarker,
    [Parameter(Mandatory=$true)][string]$EndMarker,
    [Parameter(Mandatory=$true)][string]$Payload
  )
  $managed = $StartMarker + "`n" + $Payload.TrimEnd() + "`n" + $EndMarker
  if ($Content -match [regex]::Escape($StartMarker)) {
    $pattern = [regex]::Escape($StartMarker) + '(?s).*?' + [regex]::Escape($EndMarker)
    return [regex]::Replace($Content, $pattern, $managed)
  } else {
    # Cerca un blocco già esistente di badge (prima di un titolo livello 2) oppure inserisci dopo il titolo # principale
    $lines = $Content -split "`r?`n"
    $titleIdx = -1
    for ($i=0; $i -lt $lines.Count; $i++){
      if ($lines[$i] -match '^\s*#\s+') { $titleIdx = $i; break }
    }
    if ($titleIdx -ge 0) {
      $before = $lines[0..$titleIdx]
      $after  = $lines[($titleIdx+1)..($lines.Count-1)]
      # Inserisci una riga vuota + blocco badge + riga vuota
      return ($before + @('') + @($managed) + @('') + $after) -join "`n"
    } else {
      return ($Content.TrimEnd() + "`n`n" + $managed + "`n")
    }
  }
}

# Carica README (o crea base minimale)
$content = if (Test-Path -LiteralPath $ReadmePath) {
  Get-Content -LiteralPath $ReadmePath -Raw
} else {
  "# Progesi`n"
}

# Applica blocco badges
$out = Insert-Or-Replace -Content $content -StartMarker $start -EndMarker $end -Payload $badge
$out = $out -replace "`r`n","`n"

# Scrivi in UTF-8 senza BOM
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReadmePath, $out, $enc)

Write-Host "README aggiornato con badge 'Release Health'." -ForegroundColor Green
