[CmdletBinding()]
param(
  [ValidateSet("Debug","Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Write-Host "== Progesi :: Run tests + coverage ($Configuration)" -ForegroundColor Cyan

dotnet --info | Out-Null
dotnet restore
dotnet build -c $Configuration --no-restore

$covOut = Join-Path (Get-Location) "TestResults\coverage.cobertura.xml"
dotnet test -c $Configuration --no-build `
  /p:CollectCoverage=true `
  /p:CoverletOutputFormat=cobertura `
  /p:CoverletOutput=$covOut

# Report HTML
dotnet tool update --global dotnet-reportgenerator-globaltool | Out-Null
$tool = Join-Path $env:USERPROFILE ".dotnet\tools"
$env:PATH = "$tool;$env:PATH"

$target = "coverage-html"
New-Item -ItemType Directory -Force -Path $target | Out-Null
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:$target -reporttypes:HtmlInline_AzurePipelines;Cobertura

Write-Host "HTML coverage: $target/index.html" -ForegroundColor Green
