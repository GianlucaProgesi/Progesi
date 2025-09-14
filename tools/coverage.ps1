param(
  [switch]$RunTests,
  [double]$MinLine,
  [ValidateSet('all','core','sqlite')] [string]$Scope = 'all'
)

$root = Split-Path -Parent $PSScriptRoot

$projCore  = Join-Path $root 'tests/ProgesiCore.Tests/ProgesiCore.Tests.csproj'
$projSql   = Join-Path $root 'tests/ProgesiRepositories.Sqlite.Tests/ProgesiRepositories.Sqlite.Tests.csproj'
$mergedDir = Join-Path $root 'TestResults/MergedCoverage'

function Invoke-Tests([string]$projPath) {
  $proj = (Resolve-Path $projPath).Path
  $dir  = Split-Path -Parent $proj
  Write-Host ">> dotnet test ($proj) con coverlet (Cobertura + XPlat)..."

  $outDir = Join-Path $dir 'TestResults'
  New-Item -ItemType Directory -Force -Path $outDir | Out-Null
  $outLog = Join-Path $outDir 'last.out.txt'
  $errLog = Join-Path $outDir 'last.err.txt'

  $args = @(
    'test', $proj, '-c','Debug', '-v','minimal',
    '/p:CollectCoverage=true',
    '/p:CoverletOutput=TestResults/Coverage/',
    '/p:CoverletOutputFormat=cobertura',
    '--collect:XPlat Code Coverage'
  )

  $p = Start-Process -FilePath 'dotnet' -ArgumentList $args `
       -NoNewWindow -Wait -PassThru `
       -RedirectStandardOutput $outLog `
       -RedirectStandardError  $errLog

  if ($p.ExitCode -ne 0) {
    throw "dotnet test fallito per $proj (exit $($p.ExitCode)). Vedi: $outLog / $errLog"
  }

  $covMsbuild = Join-Path $dir 'TestResults/Coverage/coverage.cobertura.xml'
  if (Test-Path $covMsbuild) { return $covMsbuild }

  $covXplat = Get-ChildItem -Path $outDir -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
              Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if ($covXplat) { return $covXplat.FullName }

  # Debug utile se non troviamo nulla
  Write-Host "Contenuto $outDir:"
  Get-ChildItem -Recurse -Path $outDir | ForEach-Object { $_.FullName } | Out-Host
  throw "Cobertura non trovati per $proj."
}

# -------------------- RUN TESTS + MERGE --------------------
if ($RunTests) {
  if (Test-Path $mergedDir) { Remove-Item $mergedDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $mergedDir | Out-Null

  $coreCov   = Invoke-Tests $projCore
  $sqliteCov = Invoke-Tests $projSql

  $reportsArg = ($coreCov, $sqliteCov) -join ';'
  & reportgenerator `
      "-reports:$reportsArg" `
      "-targetdir:$mergedDir" `
      "-reporttypes:Cobertura;TextSummary"

  $cob = Join-Path $mergedDir 'Cobertura.xml'
  if (!(Test-Path $cob)) { throw "Report merge non creato ($cob)." }

  # Mini sommario + percentuale (come prima)
  $xml = [xml](Get-Content $cob)
  $linesValid   = [int]$xml.coverage.'lines-valid'
  $linesCovered = [int]$xml.coverage.'lines-covered'
  $pct = [Math]::Round(($linesCovered / $linesValid) * 100, 2)

  Write-Host ""
  Write-Host "### Test coverage"
  Write-Host "Report: $cob`n"
  Write-Host "| Metric        | Value |"
  Write-Host "|---            | ---:  |"
  Write-Host ("| Lines covered | {0} |" -f $linesCovered)
  Write-Host ("| Lines valid   | {0}   |" -f $linesValid)
  Write-Host ("| **Line %**    | **{0}%** |" -f $pct)
  return
}

# -------------------- GATE --------------------
if ($PSBoundParameters.ContainsKey('MinLine')) {
  $cob = Join-Path $mergedDir 'Cobertura.xml'
  if (!(Test-Path $cob)) { throw "Report merged non trovato: $cob" }

  $xml = [xml](Get-Content $cob)
  $linesValid   = [int]$xml.coverage.'lines-valid'
  $linesCovered = [int]$xml.coverage.'lines-covered'
  $pct = [Math]::Round(($linesCovered / $linesValid) * 100, 2)

  Write-Host ""
  Write-Host "### Test coverage"
  Write-Host "Report: $cob`n"
  Write-Host "| Metric        | Value |"
  Write-Host "|---            | ---:  |"
  Write-Host ("| Lines covered | {0} |" -f $linesCovered)
  Write-Host ("| Lines valid   | {0}   |" -f $linesValid)
  Write-Host ("| **Line %**    | **{0}%** |" -f $pct)

  if ($pct -lt $MinLine) { throw ("Gate FALLITO: coverage {0}% < soglia {1}%." -f $pct, $MinLine) }
  else                   { Write-Host ("Gate OK: coverage {0}% >= soglia {1}%." -f $pct, $MinLine) }
}
