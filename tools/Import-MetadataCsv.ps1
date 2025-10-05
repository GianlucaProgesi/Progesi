[CmdletBinding()] param(
  [Parameter(Mandatory=$true)] [string]$DatabasePath,
  [Parameter(Mandatory=$true)] [string]$CsvPath
)
$ErrorActionPreference='Stop'

$sqlite = Join-Path $PSScriptRoot 'sqlite3\sqlite3.exe'
if (!(Test-Path $sqlite)) {
  $cmd = Get-Command sqlite3 -ErrorAction SilentlyContinue
  if ($cmd) { $sqlite = $cmd.Source }
}
if (-not (Test-Path $sqlite)) { throw "sqlite3 non trovato." }

$db = (Resolve-Path $DatabasePath).Path
if (-not (Test-Path $CsvPath)) { throw "CSV non trovato: $CsvPath" }

$first = (Get-Content -TotalCount 1 -Path $CsvPath).Trim()
if ($first -ne 'Id,Hash,By,Ref,Snips,LastModifiedUtc') {
  throw "Header CSV non valido: atteso 'Id,Hash,By,Ref,Snips,LastModifiedUtc'"
}

$rows = Import-Csv -Path $CsvPath
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('BEGIN;')
foreach($r in $rows){
  $id=[int]$r.Id
  $hash = ($r.Hash  -replace "'","''")
  $by   = ($r.By    -replace "'","''")
  $ref  = ($r.Ref   -replace "'","''")
  $sn   = ($r.Snips -replace "'","''")
  $lm   = ($r.LastModifiedUtc -replace "'","''")
  $sql = @"
INSERT INTO metadata(Id,Hash,By,Ref,Snips,LastModifiedUtc)
VALUES($id,'$hash','$by','$ref','$sn','$lm')
ON CONFLICT(Id) DO UPDATE SET
  Hash='$hash', By='$by', Ref='$ref', Snips='$sn', LastModifiedUtc='$lm';
"@
  $null = $sb.AppendLine($sql)
}
$null = $sb.AppendLine('COMMIT;')

$tmp = Join-Path $env:TEMP ("progesi_import_meta_" + [guid]::NewGuid().ToString("N") + ".sql")
[IO.File]::WriteAllText($tmp,$sb.ToString(),[Text.Encoding]::UTF8)
& $sqlite $db ".read $tmp"
Remove-Item $tmp -Force

Write-Host "IMPORT OK → $db" -ForegroundColor Green
