<#  Set-ProgesiMock.ps1
    Abilita/disabilita i mock per i componenti Metadata.

    USO:
      .\tools\Set-ProgesiMock.ps1 -On                      # abilita (root di default tests\P0-Metadata\mock)
      .\tools\Set-ProgesiMock.ps1 -On -Root 'path\ai\mock' # abilita con root specifica
      .\tools\Set-ProgesiMock.ps1 -Off                     # disabilita
      .\tools\Set-ProgesiMock.ps1                          # mostra stato
#>

param(
  [switch]$On,
  [switch]$Off,
  [string]$Root = ""
)

$ErrorActionPreference = "Stop"

# Se non passa Root → defaults alla cartella del repo
if ([string]::IsNullOrWhiteSpace($Root)) {
  $repoRoot = (Get-Location).Path
  $Root = Join-Path $repoRoot 'tests\P0-Metadata\mock'
}

if ($On -and $Off) { throw "Usa -On **oppure** -Off, non entrambi." }

function Show-State {
  $uOn   = [Environment]::GetEnvironmentVariable('PROGESI_MOCK_ON','User')
  $uRoot = [Environment]::GetEnvironmentVariable('PROGESI_MOCK_ROOT','User')
  Write-Host "User  PROGESI_MOCK_ON   = $uOn"
  Write-Host "User  PROGESI_MOCK_ROOT = $uRoot"
  Write-Host "Proc. PROGESI_MOCK_ON   = $env:PROGESI_MOCK_ON"
  Write-Host "Proc. PROGESI_MOCK_ROOT = $env:PROGESI_MOCK_ROOT"
}

if (-not $On -and -not $Off) { Show-State; exit 0 }

if ($On) {
  if (-not (Test-Path $Root)) { throw "Cartella mock non trovata: $Root" }

  # Persistenza (utente) + variabili di sessione
  [Environment]::SetEnvironmentVariable('PROGESI_MOCK_ON','1','User')
  [Environment]::SetEnvironmentVariable('PROGESI_MOCK_ROOT',$Root,'User')
  $env:PROGESI_MOCK_ON   = '1'
  $env:PROGESI_MOCK_ROOT = $Root

  Write-Host "Mock ENABLED → $Root"
  $probe = Join-Path $Root 'mock-00000001.json'
  if (-not (Test-Path $probe)) { Write-Warning "File di esempio non trovato: $probe" }
}

if ($Off) {
  [Environment]::SetEnvironmentVariable('PROGESI_MOCK_ON',$null,'User')
  [Environment]::SetEnvironmentVariable('PROGESI_MOCK_ROOT',$null,'User')
  Remove-Item Env:PROGESI_MOCK_ON   -ErrorAction SilentlyContinue
  Remove-Item Env:PROGESI_MOCK_ROOT -ErrorAction SilentlyContinue
  Write-Host "Mock DISABLED"
}

Show-State
