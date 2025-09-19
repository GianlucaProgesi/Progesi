[CmdletBinding()]
param(
  [ValidateSet("Debug","Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Write-Host "== Progesi :: Run tests + coverage ($Configuration)" -ForegroundColor Cyan

dotnet --info | Out-Null
dotnet restore
dotnet build -c $Configuration --no-restore

# Risultati in una cartella unica (come in CI)
$outDir = Join-Path (Get-Location) "out\test-results"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Usa il collector XPlat (richiede 'coverlet.collector' nei progetti di test)
dotnet test Progesi.sln -c $Configuration --no-build --logger "trx;LogFileName=test-results.trx" --results-directory "$outDir" --collect "XPlat Code Coverage"

# Verifica report Cobertura
$reports = Get-ChildItem -Recurse -Path $outDir -Filter coverage.cobertura.xml
if (-not $reports) { throw "Nessun file coverage.cobertura.xml trovato in $outDir. Verifica che i progetti di test referenzino 'coverlet.collector'." }

# Report HTML
dotnet tool update --global dotnet-reportgenerator-globaltool | Out-Null
$toolBin = Join-Path $env:USERPROFILE ".dotnet\tools"
$env:PATH = "$toolBin;$env:PATH"

$target = "coverage-html"
New-Item -ItemType Directory -Force -Path $target | Out-Null
reportgenerator -reports:"$outDir/**/coverage.cobertura.xml" -targetdir:"$target" -reporttypes:"HtmlInline_AzurePipelines;Cobertura"

Write-Host "HTML coverage: $target/index.html" -ForegroundColor Green
