param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "== Progesi preflight ==" -ForegroundColor Cyan

# Ensure we are in repo root (best effort)
if (-not (Test-Path ".git")) {
  throw "Run this script from the repo root (folder containing .git)."
}

Write-Host "`n[1/4] dotnet clean..." -ForegroundColor Yellow
dotnet clean | Out-Host

Write-Host "`n[2/4] dotnet build -c $Configuration..." -ForegroundColor Yellow
dotnet build -c $Configuration | Out-Host

Write-Host "`n[3/4] dotnet test -c $Configuration..." -ForegroundColor Yellow
dotnet test -c $Configuration | Out-Host

Write-Host "`n[4/4] git status..." -ForegroundColor Yellow
git status | Out-Host

Write-Host "`nPreflight OK." -ForegroundColor Green
