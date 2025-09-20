@'
[CmdletBinding()]
param([ValidateSet("Debug","Release")][string]$Configuration="Release")

$ErrorActionPreference="Stop"
Write-Host "== Progesi :: Run tests + coverage ($Configuration)" -ForegroundColor Cyan

dotnet restore
dotnet build -c $Configuration --no-restore

# Trova progetti di test
$testProjects = @()
if (Test-Path ".\tests") { $testProjects = Get-ChildItem .\tests -Recurse -Filter *.csproj }
if (-not $testProjects) { $testProjects = Get-ChildItem . -Recurse -Filter *.Tests.csproj }
if (-not $testProjects) { throw "Nessun progetto di test trovato." }

# Esegui test con Coverlet (Cobertura)
foreach ($proj in $testProjects) {
  Push-Location $proj.DirectoryName
  Write-Host ("-- Test: {0}" -f $proj.FullName) -ForegroundColor Cyan
  $outDir = Join-Path (Get-Location) "TestResults\coverage\"
  dotnet test $proj.Name -c $Configuration --no-build `
    /p:CollectCoverage=true `
    /p:CoverletOutputFormat=cobertura `
    /p:CoverletOutput="$outDir"
  Pop-Location
}

# Installa ReportGenerator e crea HTML
dotnet tool update --global dotnet-reportgenerator-globaltool | Out-Null
$tool = Join-Path $env:USERPROFILE ".dotnet\tools"
$env:PATH = "$tool;$env:PATH"

$target = "coverage-html"
New-Item -ItemType Directory -Force -Path $target | Out-Null

# Cerca sia nel path preciso TestResults/coverage che in fallback
$patterns = @("**/TestResults/coverage/coverage.cobertura.xml","**/coverage.cobertura.xml")
$found = @()
foreach ($p in $patterns) { $found += (Get-ChildItem -Recurse -Path . -Filter (Split-Path $p -Leaf) -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like ($p -replace "\*\*/","*") }) }

if (-not $found) { Write-Warning "Nessun report Cobertura trovato."; exit 0 }

reportgenerator `
  -reports:"**/TestResults/coverage/coverage.cobertura.xml;**/coverage.cobertura.xml" `
  -targetdir:"$target" `
  -reporttypes:"HtmlInline_AzurePipelines;Cobertura"

Write-Host "HTML coverage: $target/index.html" -ForegroundColor Green
'@ | Set-Content -Path "tools/Run-TestsWithCoverage.ps1" -Encoding utf8

# Esegui
pwsh -File .\tools\Run-TestsWithCoverage.ps1
Start-Process .\coverage-html\index.html
