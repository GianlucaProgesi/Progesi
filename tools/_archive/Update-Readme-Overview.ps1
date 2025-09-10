# tools/Update-Readme-Overview.ps1
[CmdletBinding()]
param(
  [string]$ReadmePath = './README.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Blocco Overview bilingue (stringhe doppie per evitare problemi con caratteri Unicode/apostrofi tipografici)
$blockLines = @(
  "## ℹ️ Overview",
  "",
  "**Progesi** – a modular toolchain for bridge and structural engineering:",
  "- 🧩 **Grasshopper/Rhino components** for variables, metadata, and repositories",
  "- 📦 Modular **NuGet packages** with SourceLink and built-in docs",
  "- 🚀 Automated **CI/CD pipeline** (NuGet.org + GitHub Packages)",
  "- 📝 Auto-generated **CHANGELOG** and **README** via PowerShell scripts",
  "- ✅ **Health check** and maintenance checklist for reliable releases",
  "",
  "---",
  "",
  "**Progesi** è una toolchain modulare per l’ingegneria dei ponti e delle strutture complesse:",
  "- 🧩 Componenti **Grasshopper/Rhino** per variabili, metadata e repository",
  "- 📦 Pacchetti **NuGet** modulari con SourceLink e documentazione integrata",
  "- 🚀 Pipeline **CI/CD** automatizzata (NuGet.org + GitHub Packages)",
  "- 📝 **CHANGELOG** e **README** generati automaticamente via script PowerShell",
  "- ✅ **Health check** e checklist di manutenzione per rilasci affidabili"
)
$block = $blockLines -join "`n"

$start = '<!-- PROGESI:OVERVIEW:START -->'
$end   = '<!-- PROGESI:OVERVIEW:END -->'

function Insert-Or-Replace {
  param(
    [string]$Content, [string]$StartMarker, [string]$EndMarker, [string]$Payload
  )
  $managed = $StartMarker + "`n`n" + $Payload.TrimEnd() + "`n`n" + $EndMarker

  if ($Content -match [regex]::Escape($StartMarker)) {
    $pattern = [regex]::Escape($StartMarker) + '(?s).*?' + [regex]::Escape($EndMarker)
    return [regex]::Replace($Content, $pattern, $managed)
  } else {
    # Inserisci subito dopo i badge (sezione BADGES:END), altrimenti dopo il titolo
    $lines = $Content -split "`r?`n"
    $idx = -1
    for ($i=0; $i -lt $lines.Count; $i++) {
      if ($lines[$i] -match '^<!-- PROGESI:BADGES:END -->') { $idx = $i; break }
    }
    if ($idx -eq -1) {
      for ($i=0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*#\s+') { $idx = $i; break }
      }
    }
    if ($idx -ge 0 -and $idx -lt ($lines.Count-1)) {
      $before = $lines[0..$idx]
      $after  = $lines[($idx+1)..($lines.Count-1)]
      return ($before + @('') + @($managed) + @('') + $after) -join "`n"
    } else {
      return ($Content.TrimEnd() + "`n`n" + $managed + "`n")
    }
  }
}

# Carica README
$content = if (Test-Path $ReadmePath) { Get-Content -Raw -LiteralPath $ReadmePath } else { "# Progesi" }

$out = Insert-Or-Replace -Content $content -StartMarker $start -EndMarker $end -Payload $block
$out = $out -replace "`r`n","`n"

# Scrivi UTF-8 senza BOM
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReadmePath, $out, $enc)

Write-Host "README aggiornato con sezione 'Overview' (EN + IT) subito dopo i badge." -ForegroundColor Green
