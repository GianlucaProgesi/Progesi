<#  Prepare-P6-Sqlite.ps1
    Crea un DB SQLite “progesi_p6.db” con 3 righe uguali ai mock 00000001/2/3.
#>

[CmdletBinding()]
param(
  [string]$OutDir = "$PSScriptRoot\..\tests\P6-live",
  [string]$DbName = "progesi_p6.db"
)

$ErrorActionPreference = "Stop"

# 1) Assicura cartella
if (-not (Test-Path -LiteralPath $OutDir)) {
  New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
}
$root = (Resolve-Path -LiteralPath $OutDir).Path
$db   = Join-Path $root $DbName

# 2) SQL
$sql = @"
DROP TABLE IF EXISTS metadata;
CREATE TABLE metadata(
  id        INTEGER PRIMARY KEY,
  hash      TEXT    NOT NULL,
  by_author TEXT    NOT NULL,
  refs      TEXT    NOT NULL,
  snips     TEXT    NOT NULL,
  lastmod   TEXT    NOT NULL
);

INSERT INTO metadata(id,hash,by_author,refs,snips,lastmod) VALUES
(1,'mock-00000001','GM',
 'https://example.org/metadata/1|file:///C:/Progesi/mock/1.png',
 'snip:1:image/png:caption=Mock-1',
 '2025-09-29T01:00:00Z'),
(2,'mock-00000002','GM',
 'https://example.org/metadata/2|file:///C:/Progesi/mock/2.png',
 'snip:2:image/png:caption=Mock-2',
 '2025-09-29T01:00:00Z'),
(3,'mock-00000003','GM',
 'https://example.org/metadata/3|file:///C:/Progesi/mock/3.png',
 'snip:3:image/png:caption=Mock-3',
 '2025-09-29T01:00:00Z');
"@

# 3) Trova sqlite3: prima in tools\sqlite3\sqlite3.exe poi nel PATH
$localSqlite = Join-Path $PSScriptRoot "sqlite3\sqlite3.exe"
$sqlite3 = $null
if (Test-Path $localSqlite) { $sqlite3 = Get-Item $localSqlite }
else { $sqlite3 = (Get-Command sqlite3 -ErrorAction SilentlyContinue) }

if ($sqlite3) {
  if (Test-Path $db) { Remove-Item $db -Force }
  $sql | & $sqlite3 $db
  Write-Host "DB creato con sqlite3 → $db"
}
else {
  # 4) Fallback ADO.NET (se hai l’assembly installato nel GAC o copiato nella sessione)
  try {
    Add-Type -AssemblyName System.Data.SQLite -ErrorAction Stop
    if (Test-Path $db) { Remove-Item $db -Force }
    $cn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$db;Version=3;")
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.ExecuteNonQuery()
    $cn.Close()
    Write-Host "DB creato via System.Data.SQLite → $db"
  }
  catch {
    throw "Manca sqlite3 ed anche 'System.Data.SQLite'. Soluzioni:
 - Installa sqlite3 (winget/choco/scoop) e riprova;
 - Oppure copia sqlite3.exe in 'tools\sqlite3\' (portable) e riprova."
  }
}

Write-Host "OK. Percorso DB: $db"
