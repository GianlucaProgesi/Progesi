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

$db  = (Resolve-Path $DatabasePath).Path
$outDir = Split-Path $CsvPath -Parent
if (-not (Test-Path $outDir)) { New-Item -Type Directory -Force -Path $outDir | Out-Null }

$rows = & $sqlite $db "SELECT Id,Hash,By,Ref,Snips,LastModifiedUtc FROM metadata ORDER BY Id;"
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('Id,Hash,By,Ref,Snips,LastModifiedUtc')
foreach($line in $rows){
  $parts = $line -split '\|',6
  for($i=0;$i -lt $parts.Length;$i++){ $parts[$i] = '"' + ($parts[$i] -replace '"','""') + '"' }
  $null = $sb.AppendLine(($parts -join ','))
}
[IO.File]::WriteAllText($CsvPath,$sb.ToString(),[Text.Encoding]::UTF8)
Write-Host "EXPORT → $CsvPath" -ForegroundColor Green
