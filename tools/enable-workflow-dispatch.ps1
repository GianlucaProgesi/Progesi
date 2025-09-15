# tools/enable-workflow-dispatch.ps1
[CmdletBinding(SupportsShouldProcess=$true)]
param(
  [string]$WorkflowsDir = ".github/workflows"
)
$ErrorActionPreference = 'Stop'

function Add-Dispatch($text) {
  if ($text -match '(?im)^\s*workflow_dispatch\s*:') { return $text }

  # on: [ ... ]
  if ($text -match '(?im)^\s*on\s*:\s*\[(.*?)\]') {
    $inside = $Matches[1]
    if ($inside -notmatch '(?i)workflow_dispatch') {
      $inside2 = ($inside -replace '\s+', ' ').Trim()
      if ($inside2.Length -eq 0) { $inside2 = 'workflow_dispatch' }
      else { $inside2 = "$inside2, workflow_dispatch" }
      return ($text -replace '(?im)^\s*on\s*:\s*\[.*?\]', "on: [$inside2]")
    }
    return $text
  }

  # on: { ... }
  if ($text -match '(?im)^\s*on\s*:\s*\{(.*?)\}') {
    $inside = $Matches[1]
    if ($inside -notmatch '(?i)workflow_dispatch') {
      $inside2 = $inside.Trim()
      if ($inside2.Length -eq 0) { $inside2 = 'workflow_dispatch: {}' }
      else { $inside2 = "$inside2, workflow_dispatch: {}" }
      return ($text -replace '(?im)^\s*on\s*:\s*\{.*?\}', "on: {$inside2}")
    }
    return $text
  }

  # on: push
  if ($text -match '(?im)^\s*on\s*:\s*([A-Za-z_]+)\s*$') {
    $ev = $Matches[1]
    if ($ev -notmatch '(?i)workflow_dispatch') {
      return ($text -replace '(?im)^\s*on\s*:\s*[A-Za-z_]+\s*$', "on: [$ev, workflow_dispatch]")
    }
    return $text
  }

  # on:\n  push:\n...
  if ($text -match '(?im)^\s*on\s*:\s*(\r?\n)+') {
    return ($text -replace '(?im)^(on\s*:\s*(\r?\n)+)', "`$1  workflow_dispatch:`r`n")
  }

  # Nessun on: → prepend
  return "on:`r`n  workflow_dispatch:`r`n$text"
}

$dir = Join-Path (Get-Location) $WorkflowsDir
if (-not (Test-Path -LiteralPath $dir)) {
  Write-Warning "Cartella non trovata: $dir"
  exit 0
}

# ⚠️ FIX: usa wildcard nel Path (oppure fai due passaggi separati)
$files = Get-ChildItem -Path "$dir\*.yml","$dir\*.yaml" -File | Sort-Object Name

foreach ($f in $files) {
  $p = $f.FullName
  $txt = Get-Content -LiteralPath $p -Raw
  $new = Add-Dispatch $txt

  if ($new -ne $txt) {
    if ($PSCmdlet.ShouldProcess($p,'Add workflow_dispatch')) {
      Copy-Item -LiteralPath $p -Destination ($p + '.bak') -Force
      Set-Content -LiteralPath $p -Value $new -Encoding UTF8
      Write-Host ("{0,-35}  {1}" -f $f.Name,'AGGIUNTO')
    } else {
      Write-Host ("{0,-35}  {1}" -f $f.Name,'AGGIUNTO (WhatIf)')
    }
  } else {
    Write-Host ("{0,-35}  {1}" -f $f.Name,'OK (nessuna modifica)')
  }
}

Write-Host "`nVerifica:"
Get-ChildItem -Path "$dir\*.yml","$dir\*.yaml" -File | ForEach-Object {
  $c = Get-Content -LiteralPath $_.FullName -Raw
  $status = if ($c -match '(?im)^\s*workflow_dispatch\s*:') {'OK'} else {'MANCANTE'}
  '{0,-35}  {1}' -f $_.Name, $status
}
