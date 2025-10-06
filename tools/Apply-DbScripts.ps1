
Param(
  [Parameter(Mandatory=$true)] [string]$DbPath,
  [string]$ScriptsFolder = 'tools/sql',
  [switch]$Idempotent
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DbPath)) { throw "DB non trovato: $DbPath" }
if (-not (Test-Path $ScriptsFolder)) { throw "Cartella script non trovata: $ScriptsFolder" }

function Invoke-WithSqlite3([string]$db, [string]$sql) {
  $sqlite3 = (Get-Command sqlite3 -ErrorAction SilentlyContinue).Source
  if (-not $sqlite3) { return $false }
  $tmp = New-TemporaryFile
  Set-Content -Path $tmp -Value $sql -Encoding UTF8
  & $sqlite3 $db < $tmp  | Out-Null
  Remove-Item $tmp -Force
  return $true
}

function Invoke-WithProvider([string]$db, [string]$sql) {
  Add-Type -AssemblyName System.Data
  try {
    Add-Type -Path (Join-Path $PSScriptRoot 'lib/System.Data.SQLite.dll') -ErrorAction Stop
  } catch {
    throw "sqlite3.exe non trovato e provider System.Data.SQLite non disponibile. Fornire tools/lib/System.Data.SQLite.dll oppure installare sqlite3."
  }
  $cn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db;Version=3;")
  $cn.Open()
  $cmd = $cn.CreateCommand()
  $cmd.CommandText = $sql
  $cmd.ExecuteNonQuery() | Out-Null
  $cn.Close()
}

# Legge ogni .sql e lo esegue statement per statement (separatore ;) con tolleranza ad errori comuni
$scripts = Get-ChildItem -Path $ScriptsFolder -Filter '*.sql' | Sort-Object Name
if ($scripts.Count -eq 0) { Write-Host 'Nessuno script SQL da applicare.'; exit 0 }

foreach ($s in $scripts) {
  Write-Host "Applying: $($s.Name)"
  $raw = Get-Content $s.FullName -Raw
  $stmts = $raw -split ';\s*(\r?\n|$)'
  foreach ($stmt in $stmts) {
    $sql = $stmt.Trim()
    if (-not $sql) { continue }
    try {
      if (-not (Invoke-WithSqlite3 -db $DbPath -sql $sql)) {
        Invoke-WithProvider -db $DbPath -sql $sql
      }
    } catch {
      if ($Idempotent) {
        $msg = $_.Exception.Message
        if ($msg -match 'duplicate column name' -or $msg -match 'already exists' -or $msg -match 'no such column: Ref') {
          Write-Host "Skip (idempotent): $($msg)"
          continue
        }
      }
      throw
    }
  }
}

Write-Host 'Script DB applicati con successo.'
