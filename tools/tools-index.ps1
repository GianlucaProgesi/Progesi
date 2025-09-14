<# 
.SYNOPSIS
Elenca gli script in ./tools con una breve descrizione (se presente).
#>
[CmdletBinding()]
param([switch]$Long)

$here = Split-Path -Parent $PSCommandPath
$tools = Get-ChildItem -Path $here -Filter *.ps1 -File | Sort-Object Name

function Get-Summary([string]$path) {
  $txt = Get-Content -LiteralPath $path -Raw
  if ($txt -match '<#(.+?)#>') {
    $block = $Matches[1]
    if ($block -match '(?ms)^\s*\.SYNOPSIS\s*(.+)$') {
      return (($Matches[1] -replace '\r','').Trim() -split '\n')[0]
    }
  }
  foreach ($line in ($txt -split '\r?\n')) {
    if ($line -match '^\s*#\s*(.+)') { return $Matches[1].Trim() }
    if ($line.Trim().Length -gt 0 -and $line -notmatch '^\s*#') { break }
  }
  return ''
}

"Tools in ./tools`n"
foreach ($t in $tools) {
  $sum = Get-Summary $t.FullName
  if ($Long) { "{0,-28}  {1}" -f $t.Name,$sum } else { "{0,-28}  {1}" -f $t.BaseName,$sum }
}
"`nEsegui: pwsh ./tools/<nome>.ps1 -? (se supportato) oppure apri lo script per i parametri."
