<#
Set-ProgesiLive.ps1
Accende/Spegne la modalità LIVE e imposta/mostra la posizione del DB.

USO:
  .\tools\Set-ProgesiLive.ps1 -On  -DatabasePath ".\tests\P6-live\progesi_p6.db"
  .\tools\Set-ProgesiLive.ps1 -Off
  .\tools\Set-ProgesiLive.ps1 -Show
#>

[CmdletBinding()]
param(
  [switch]$On,
  [switch]$Off,
  [switch]$Show,
  [Parameter(Mandatory=$false)]
  [string]$DatabasePath  # nessun alias (evita collisioni con -Debug)
)

function Set-UserVar([string]$name, [string]$value) {
  # livello UTENTE
  if ([string]::IsNullOrEmpty($value)) {
    [Environment]::SetEnvironmentVariable($name, $null, "User")
  } else {
    [Environment]::SetEnvironmentVariable($name, $value, "User")
  }
  # livello PROCESSO (di questa shell)
  if ([string]::IsNullOrEmpty($value)) {
    Remove-Item "Env:$name" -ErrorAction SilentlyContinue
  } else {
    Set-Item -Path "Env:$name" -Value $value
  }
}

if ($Show) {
  "User  PROGESI_LIVE_ON   = " + ([Environment]::GetEnvironmentVariable("PROGESI_LIVE_ON", "User"))
  "User  PROGESI_LIVE_DB   = " + ([Environment]::GetEnvironmentVariable("PROGESI_LIVE_DB", "User"))
  "Proc. PROGESI_LIVE_ON   = $($env:PROGESI_LIVE_ON)"
  "Proc. PROGESI_LIVE_DB   = $($env:PROGESI_LIVE_DB)"
  return
}

if ($On) {
  # spegni qualunque mock residuo
  foreach($m in "PROGESI_ENABLE_MOCK","PROGESI_MOCK_ON","PROGESI_MOCK_ROOT",
             "PROGESI_METADATA_MOCK","PROGESI_METADATA_MOCK_DIR","PROGESI_METADATA_FIXTURES") {
    Set-UserVar $m $null
  }

  if (-not $DatabasePath) {
    throw "Manca -DatabasePath (es: .\tests\P6-live\progesi_p6.db)"
  }
  $dbFull = (Resolve-Path $DatabasePath).Path
  if (-not (Test-Path $dbFull)) { throw "DB non trovato: $dbFull" }

  Set-UserVar "PROGESI_LIVE_ON" "1"
  Set-UserVar "PROGESI_LIVE_DB" $dbFull

  "Live ENABLED -> $dbFull"
  "User  PROGESI_LIVE_ON   = " + ([Environment]::GetEnvironmentVariable("PROGESI_LIVE_ON", "User"))
  "User  PROGESI_LIVE_DB   = " + ([Environment]::GetEnvironmentVariable("PROGESI_LIVE_DB", "User"))
  "Proc. PROGESI_LIVE_ON   = $($env:PROGESI_LIVE_ON)"
  "Proc. PROGESI_LIVE_DB   = $($env:PROGESI_LIVE_DB)"
  return
}

if ($Off) {
  Set-UserVar "PROGESI_LIVE_ON" $null
  Set-UserVar "PROGESI_LIVE_DB" $null
  "Live DISABLED"
  return
}

throw "Nessuna azione. Usa -On/-Off/-Show."
