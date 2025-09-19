[CmdletBinding()]
param(
  [ValidateSet("Debug","Release")][string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
Write-Host "== Progesi :: Run tests + coverage ($Configuration)" -ForegroundColor Cyan

# Build soluzione
dotnet --info | Out-Null
dotnet restore
dotnet build -c $Configuration --no-restore

# Trova i progetti di test (prima sotto ./tests, poi fallback ovunque con *.Tests.csproj)
$testProjects = @()
if (Test-Path ".\tests") {
  $testProjects = Get-ChildItem .\tests -Recurse -Filter *.csproj
}
if (-not $testProjects -or $testProjects.Count -eq 0) {
  $testProjects = Get-ChildItem . -Recurse -Filter *.Tests.csproj
}
if (-not $testProjects -or $testProjects.Count -eq 0) {
  throw "Nessun progetto di test trovato."
}

# Esegui test per progetto e scrivi la coverage SOTTO ciascun progetto (./TestResults/coverage.cobertura.xml)
foreach ($proj in $testProjects) {
  Push-Location $proj.D
