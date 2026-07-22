param(
    [string]$BaseBranch = "release/v0.9.0-beta",
    [string]$FeatureBranch = "feat/axis-variable-core"
)

$ErrorActionPreference = "Stop"

Write-Host "== Progesi: start AxisVariable feature ==" -ForegroundColor Cyan

# assicurati di partire da base pulita
git checkout $BaseBranch
git pull

# crea la nuova branch
git checkout -b $FeatureBranch

Write-Host "Branch '$FeatureBranch' created from '$BaseBranch'" -ForegroundColor Green

# sanity check
git status
