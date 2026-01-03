param(
  [string]$Branch = "",
  [string]$Configuration = "Release",
  [string]$Message = "feat(cluster): ClusterDef Act + ClusterOut dynamic outputs + DataEx cluster import/export + hardening"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path ".git")) {
  throw "Run this script from the repo root (folder containing .git)."
}

Write-Host "== Progesi commit & push ==" -ForegroundColor Cyan

# Optional branch creation/switch
if (-not [string]::IsNullOrWhiteSpace($Branch)) {
  $current = (git rev-parse --abbrev-ref HEAD).Trim()
  if ($current -ne $Branch) {
    $exists = (git branch --list $Branch)
    if ([string]::IsNullOrWhiteSpace($exists)) {
      Write-Host "Creating branch '$Branch'..." -ForegroundColor Yellow
      git checkout -b $Branch | Out-Host
    } else {
      Write-Host "Switching to branch '$Branch'..." -ForegroundColor Yellow
      git checkout $Branch | Out-Host
    }
  }
}

# Preflight
Write-Host "`nRunning preflight (build/test)..." -ForegroundColor Yellow
dotnet clean | Out-Host
dotnet build -c $Configuration | Out-Host
dotnet test -c $Configuration | Out-Host

# Stage
Write-Host "`nStaging changes..." -ForegroundColor Yellow
git add -A | Out-Host

Write-Host "`nGit status (staged)..." -ForegroundColor Yellow
git status | Out-Host

# Commit
Write-Host "`nCommitting..." -ForegroundColor Yellow
git commit -m $Message | Out-Host

# Push
$cur = (git rev-parse --abbrev-ref HEAD).Trim()
Write-Host "`nPushing branch '$cur'..." -ForegroundColor Yellow
git push -u origin $cur | Out-Host

Write-Host "`nDone." -ForegroundColor Green
