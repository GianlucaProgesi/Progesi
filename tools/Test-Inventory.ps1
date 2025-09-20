[CmdletBinding()]
param(
  [ValidateSet("Debug","Release")][string]$Configuration = "Release",
  [string]$OutDir = "out\test-inventory"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# Progetti di test: cartella \Tests\ o riferimento xunit
$csprojs = Get-ChildItem . -Recurse -Filter *.csproj |
  Where-Object {
    $_.FullName -match '\\Tests\\' -or
    (Get-Content $_.FullName -Raw) -match '<PackageReference Include="xunit'
  }

if (-not $csprojs) { Write-Warning "Nessun progetto di test trovato."; return }

# build una sola volta la solution per la config richiesta
dotnet build -c $Configuration | Out-Null

$summary = @()

foreach($p in $csprojs) {
  $name = [IO.Path]::GetFileNameWithoutExtension((Split-Path $p.FullName -Leaf))
  $outList = Join-Path $OutDir "$name.list.txt"
  $trx     = Join-Path $OutDir "$name.trx"

  Write-Host "→ [$name] elenco test ($Configuration)..."

  # 1) tentativo: list senza esecuzione
  dotnet test $p.FullName -c $Configuration --no-build --list-tests > $outList 2>&1

  # conta linee “tipo test” (molto permissivo)
  $count = 0
  if (Test-Path $outList) {
    $lines = Get-Content $outList -ErrorAction SilentlyContinue
    $count = ($lines | Where-Object {
      $_.Trim() -ne "" -and
      $_ -notmatch "Test run for" -and
      $_ -notmatch "Starting test execution" -and
      $_ -notmatch "Microsoft \(R\) Test" -and
      $_ -notmatch "The following Tests are available" -and
      $_ -notmatch "Informational" -and
      $_ -notmatch "Warning"
    }).Count
  }

  # 2) fallback: esegui davvero e conta dal TRX
  if ($count -eq 0) {
    Write-Host "   Nessun output utile da --list-tests, eseguo fallback TRX..." -ForegroundColor DarkGray
    Remove-Item $trx -ErrorAction SilentlyContinue
    dotnet test $p.FullName -c $Configuration --no-build --logger "trx;LogFileName=$trx" > $null
    if (Test-Path $trx) {
      $count = (Select-String -Path $trx -Pattern '<UnitTest\s' -AllMatches |
                Select-Object -Expand Matches | Measure-Object).Count
      if ($count -eq 0) {
        # alcuni runner mettono solo UnitTestResult
        $count = (Select-String -Path $trx -Pattern '<UnitTestResult\s' -AllMatches |
                  Select-Object -Expand Matches | Measure-Object).Count
      }
    }
  }

  $summary += [pscustomobject]@{
    Project = $name; Count = $count; ListFile = $outList; TrxFile = $trx; Config = $Configuration
  }
}

$summary | Sort-Object Project | Format-Table -AutoSize
$csv = Join-Path $OutDir "summary.csv"
$summary | Export-Csv $csv -NoTypeInformation -Encoding UTF8
Write-Host "Report: $csv"
