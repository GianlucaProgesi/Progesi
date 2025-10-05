<# Import-VariablesCsv.ps1
   Importa/Upsert da CSV nella tabella "variables" del DB SQLite.

CSV atteso (header esatto):
  Id,Hash,Name,Value,Unit,By,LastModifiedUtc

USO:
  .\tools\Import-VariablesCsv.ps1 -DatabasePath ".\tests\P6-live\progesi_p6.db" -CsvPath ".\export\variables.csv"
#>

[CmdletBinding()] param(
  [Parameter(Mandatory=$true)] [string]$DatabasePath,
  [Parameter(Mandatory=$true)] [string]$CsvPath
)
$ErrorActionPreference = 'Stop'

# sqlite3 portabile (tools\sqlite3) o PATH
$sqlite = Join-Path $PSScriptRoot 'sqlite3\sqlite3.exe'
if (!(Test-Path $sqlite)) {
  $cmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
  if ($cmd) { $sqlite = $cmd.Source }
}
if (-not (Test-Path $sqlite)) { throw "sqlite3 non trovato. Metti sqlite3.exe in tools\sqlite3 oppure nel PATH." }

$db  = (Resolve-Path $DatabasePath).Path
$csv = (Resolve-Path $CsvPath).Path
if (!(Test-Path $db))  { throw "DB non trovato: $db" }
if (!(Test-Path $csv)) { throw "CSV non trovato: $csv" }

# Valida header
$first = (Get-Content -TotalCount 1 -Path $csv).Trim()
if ($first -ne 'Id,Hash,Name,Value,Unit,By,LastModifiedUtc') {
  throw "Header CSV non valido: atteso 'Id,Hash,Name,Value,Unit,By,LastModifiedUtc'"
}

# Import CSV
$rows = Import-Csv -Path $csv

# Costruisci uno script SQL con upsert
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('BEGIN;')

foreach($r in $rows) {
  $id    = [int]$r.Id

  $hash  = if ($null -eq $r.Hash)  { '' } else { $r.Hash  };  $hash  = $hash.Replace("'", "''")
  $name  = if ($null -eq $r.Name)  { '' } else { $r.Name  };  $name  = $name.Replace("'", "''")
  $value = if ($null -eq $r.Value) { '' } else { $r.Value };  $value = $value.Replace("'", "''")
  $unit  = if ($null -eq $r.Unit)  { '' } else { $r.Unit  };  $unit  = $unit.Replace("'", "''")
  $by    = if ($null -eq $r.By)    { '' } else { $r.By    };  $by    = $by.Replace("'", "''")
  $lm    = if ($null -eq $r.LastModifiedUtc) { '' } else { $r.LastModifiedUtc }; $lm = $lm.Replace("'", "''")

  $sql = @"
INSERT INTO variables(Id,Hash,Name,Value,Unit,By,LastModifiedUtc)
VALUES($id,'$hash','$name','$value','$unit','$by','$lm')
ON CONFLICT(Id) DO UPDATE SET
  Hash='$hash', Name='$name', Value='$value', Unit='$unit', By='$by', LastModifiedUtc='$lm';
"@
  $null = $sb.AppendLine($sql)
}

$null = $sb.AppendLine('COMMIT;')

# Esegui tramite file temporaneo
$tmp = Join-Path $env:TEMP ("progesi_import_var_" + [guid]::NewGuid().ToString("N") + ".sql")
[IO.File]::WriteAllText($tmp,$sb.ToString(),[Text.Encoding]::UTF8)
& $sqlite $db ".read $tmp"
Remove-Item $tmp -Force

Write-Host "IMPORT OK → $db" -ForegroundColor Green
