# Utility di diagnostica per le run di GitHub Actions (Windows)
# Safe per variabili dinamiche e percorsi con caratteri speciali.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "==============================" 
Write-Host "== dotnet --info"
Write-Host "=============================="
& dotnet --info

Write-Host ""
Write-Host "=============================="
Write-Host "== Selected environment variables"
Write-Host "=============================="
$vars = @(
  'ImageOS','RUNNER_OS','PROCESSOR_ARCHITECTURE',
  'DOTNET_ROOT','DOTNET_ROOT_x64','DOTNET_MULTILEVEL_LOOKUP',
  'GITHUB_REF','GITHUB_SHA','GITHUB_EVENT_NAME','GITHUB_WORKFLOW',
  'Configuration','SLNF'
)
foreach ($name in $vars) {
  $val = [System.Environment]::GetEnvironmentVariable($name)
  Write-Host ("{0}={1}" -f $name, $val)
}

Write-Host ""
Write-Host "=============================="
Write-Host "== Repository tree (first 2 levels)"
Write-Host "=============================="
Get-ChildItem -Force -Depth 2 | ForEach-Object {
  $p = $_.FullName.Replace((Get-Location).Path, ".")
  Write-Host $p
}

Write-Host ""
Write-Host "=============================="
Write-Host "== TestResults (paths)"
Write-Host "=============================="
Get-ChildItem -Recurse -Force "$PWD/TestResults" -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Host $_.FullName
}

Write-Host ""
Write-Host "=============================="
Write-Host "== Coverage files (cobertura)"
Write-Host "=============================="
Get-ChildItem -Recurse -Force "$PWD/TestResults" -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue | ForEach-Object {
  Write-Host $_.FullName
}
