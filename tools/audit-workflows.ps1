[CmdletBinding()]
param()
$dir = Join-Path (Get-Location) ".github\workflows"
if (-not (Test-Path $dir)) { Write-Host "Nessuna cartella $dir"; exit 0 }

Get-ChildItem $dir -File -Include *.yml,*.yaml | ForEach-Object {
  $txt = Get-Content -LiteralPath $_.FullName -Raw
  [pscustomobject]@{
    File         = $_.Name
    HasDispatch  = [bool]($txt -match '(?im)^\s*workflow_dispatch\s*:')
    HasOnKey     = [bool]($txt -match '(?im)^\s*(?:[''"]?on[''"]?)\s*:')
  }
} | Sort-Object File | Format-Table -AutoSize
