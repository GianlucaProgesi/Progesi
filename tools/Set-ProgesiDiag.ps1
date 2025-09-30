<#  Set-ProgesiDiag.ps1
    Abilita/Disabilita la diagnostica dei componenti Progesi (solo variabili d’ambiente).
    Uso:
      .\tools\Set-ProgesiDiag.ps1 -On
      .\tools\Set-ProgesiDiag.ps1 -Off
#>

[CmdletBinding()]
param(
  [switch]$On,
  [switch]$Off
)

function Set-UserEnv([string]$name, [string]$value) {
  [Environment]::SetEnvironmentVariable($name, $value, 'User')
}

if ($On) {
  Set-UserEnv 'PROGESI_DIAG_ON' '1'
  Write-Host "Diag ENABLED → PROGESI_DIAG_ON=1 (User)"
  Write-Host "Chiudi e riapri Rhino/VS per applicare."
  exit 0
}
elseif ($Off) {
  Set-UserEnv 'PROGESI_DIAG_ON' $null
  Write-Host "Diag DISABLED → PROGESI_DIAG_ON cleared (User)"
  Write-Host "Chiudi e riapri Rhino/VS per applicare."
  exit 0
}
else {
  $v = [Environment]::GetEnvironmentVariable('PROGESI_DIAG_ON','User')
  if ([string]::IsNullOrWhiteSpace($v)) { $v = '(unset)' }
  Write-Host "Current  PROGESI_DIAG_ON = $v"
  Write-Host "Usa -On oppure -Off."
}
