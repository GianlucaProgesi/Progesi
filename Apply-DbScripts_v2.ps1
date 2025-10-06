
Param(
  [Parameter(Mandatory=$true)] [string]$DbPath,
  [string]$ScriptsFolder = 'tools/sql',
  [switch]$Idempotent
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DbPath)) { throw "DB non trovato: $DbPath" }
if (-not (Test-Path $ScriptsFolder)) { throw "Cartella script non trovata: $ScriptsFolder" }

function Get-Sqlite3Path {
  try {
    $c = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
  } catch {}
  return $null
}

function Invoke-WithSqlite3([string]$db, [string]$sqlFilePath) {
  $sqlite3 = Get-Sqlite3Path
  if (-not $sqlite3) { return $false }
  & $sqlite3 $db (".read " + $sqlFilePath)
  return $true
}

function Invoke-WithProvider([string]$db, [string]$sqlText) {
  Add-Type -AssemblyName System.Data
  try {
    $lib = Join-Path $PSScriptRoot 'lib/System.Data.SQLite.dll'
    if (-not (Test-Path $lib)) {
      throw "Provider System.Data.SQLite non disponibile (tools\lib\System.Data.SQLite.dll mancante)."
    }
    Add-Type -Path $lib -ErrorAction Stop
  } catch {
    throw "sqlite3.exe non trovato e provider System.Data.SQLite non disponibile. Installa sqlite3 o fornisci tools\lib\System.Data.SQLite.dll"
  }
  $cn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db;Version=3;")
  $cn.Open()
  $cmd = $cn.CreateCommand()
  $cmd.CommandText = $sqlText
  $cmd.ExecuteNonQuery() | Out-Null
  $cn.Close()
}

$scripts = Get-ChildItem -Path $ScriptsFolder -Filter '*.sql' | Sort-Object Name
if ($scripts.Count -eq 0) { Write-Host 'Nessuno script SQL da applicare.'; exit 0 }

foreach ($s in $scripts) {
  Write-Host ("Applying: {0}" -f $s.Name)
  $sql = Get-Content $s.FullName -Raw

  $ran = $false
  if (Get-Sqlite3Path) {
    try {
      Invoke-WithSqlite3 -db $DbPath -sqlFilePath $s.FullName
      $ran = $true
    } catch {
      if (-not $Idempotent) { throw }
      Write-Host ("Skip by sqlite3 (idempotent?): {0}" -f $_.Exception.Message)
    }
  }

  if (-not $ran) {
    try {
      Invoke-WithProvider -db $DbPath -sqlText $sql
      $ran = $true
    } catch {
      if ($Idempotent -and ($_.Exception.Message -match 'already exists' -or $_.Exception.Message -match 'duplicate column name')) {
        Write-Host ("Skip (idempotent): {0}" -f $_.Exception.Message)
        $ran = $true
      } else {
        throw
      }
    }
  }
}

Write-Host 'Script DB applicati con successo.'
