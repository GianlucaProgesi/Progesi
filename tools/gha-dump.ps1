# tools/gha-dump.ps1
$ErrorActionPreference = 'Continue'

Write-Host "=== DOTNET INFO ==="
dotnet --info
dotnet --list-sdks
dotnet --list-runtimes

Write-Host "`n=== REPO LAYOUT (depth 2) ==="
Get-ChildItem -Recurse -Depth 2 | Select-Object -ExpandProperty FullName

Write-Host "`n=== SLNF / SLN ==="
Get-ChildItem -Recurse -Filter *.sln,*.slnf | ForEach-Object { $_.FullName }

Write-Host "`n=== LOCK FILES ==="
Get-ChildItem -Recurse -Filter packages.lock.json | ForEach-Object { $_.FullName }

Write-Host "`n=== TEST RESULTS ==="
Get-ChildItem -Recurse TestResults -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }

Write-Host "`n=== GITHUB CONTEXT (BASIC) ==="
$vars = @(
  "GITHUB_WORKFLOW","GITHUB_RUN_ID","GITHUB_RUN_NUMBER","GITHUB_JOB",
  "GITHUB_EVENT_NAME","GITHUB_REF","GITHUB_SHA","GITHUB_REPOSITORY",
  "GITHUB_ACTOR","RUNNER_OS","RUNNER_TEMP","RUNNER_WORKSPACE"
)
$vars | ForEach-Object { "{0}={1}" -f $_, $Env:$_ } | ForEach-Object { Write-Host $_ }

Write-Host "`n=== DONE ==="
