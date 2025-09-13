# tools/gha-dump.ps1
[CmdletBinding()]
param(
    [string[]] $Vars = @(
        'ImageOS','RUNNER_OS','PROCESSOR_ARCHITECTURE',
        'DOTNET_ROOT','DOTNET_ROOT_x64','DOTNET_MULTILEVEL_LOOKUP',
        'GITHUB_REF','GITHUB_SHA','GITHUB_EVENT_NAME','GITHUB_WORKFLOW',
        'Configuration','SLNF'
    )
)

function Write-Section($title) {
    Write-Host ""
    Write-Host "=============================="
    Write-Host "== $title"
    Write-Host "=============================="
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

Write-Section "dotnet --info"
dotnet --info

Write-Section "Selected environment variables"
$Vars | ForEach-Object {
    $val = [Environment]::GetEnvironmentVariable($_)
    "{0}={1}" -f $_, $val
}

Write-Section "Repository tree (first 2 levels)"
Get-ChildItem -Recurse -Depth 2 | ForEach-Object {
    $_.FullName.Replace((Get-Location).Path, '.')
}

Write-Section "TestResults (paths)"
Get-ChildItem -Recurse -ErrorAction SilentlyContinue "TestResults" | ForEach-Object { $_.FullName }

Write-Section "Coverage files (cobertura)"
Get-ChildItem -Recurse -ErrorAction SilentlyContinue "coverage.cobertura.xml" | ForEach-Object { $_.FullName }
