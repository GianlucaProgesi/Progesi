<# Export-VariablesCsv.ps1
   Esporta la tabella "variables" del DB SQLite in un file CSV (UTF-8).

USO:
  .\tools\Export-VariablesCsv.ps1 -DatabasePath ".\tests\P6-live\progesi_p6.db" -CsvPath ".\export\variables.csv"
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
$out = (Resolve-Path (Split-Path $CsvPath -Parent) -ErrorAction SilentlyContinue)
if (-not $out) { New-Item -ItemType Directory -Force -Path (Split-Path $CsvPath -Parent) | Out-Null }

# Query
$rows = & $sqlite $db "SELECT Id,Hash,Name,Value,Unit,By,LastModifiedUtc FROM variables ORDER BY Id;"

# Scrittura CSV manuale (quoting sicuro)
$nl = [Environment]::NewLine
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('Id,Hash,Name,Value,Unit,By,LastModifiedUtc')
foreach($line in $rows){
  $parts = $line -split '\|',7
  for($i=0;$i -lt $parts.Length;$i++){ $parts[$i] = '"' + ($parts[$i] -replace '"','""') + '"' }
  $null = $sb.AppendLine( ($parts -join ',') )
}

[IO.File]::WriteAllText($CsvPath,$sb.ToString(),[Text.Encoding]::UTF8)
Write-Host "EXPORT → $CsvPath" -ForegroundColor Green
