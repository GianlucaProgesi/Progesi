Param(
  [string]$Repo  = "GianlucaProgesi/Progesi",
  [string]$Base  = "main"
)

$ErrorActionPreference = "Stop"

function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# 0) gh presente + login
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail "Installa GitHub CLI: https://cli.github.com/"; exit 1 }
gh auth status 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { gh auth login -w; gh auth status | Out-Null }

# 1) Crea un branch temporaneo da main
git fetch origin
$ts = (Get-Date -Format "yyyyMMdd-HHmmss")
$branch = "chore/seed-required-checks-$ts"
git switch -c $branch origin/$Base

# 2) Commit “innocuo” per far partire CI sulla PR
New-Item -ItemType Directory -Force -Path .github | Out-Null
"seed $ts" | Set-Content .github\ci-seed.txt -Encoding UTF8
git add .github\ci-seed.txt
git commit -m "chore(ci): seed CI check ($ts)"
git push -u origin $branch

# 3) Apri PR (draft) verso main
$prUrl = gh pr create --repo $Repo --base $Base --head $branch `
  --title "chore(ci): seed CI check ($ts)" `
  --body "PR temporanea per far comparire 'CI / test (pull_request)' tra i Required checks." `
  --draft
Ok "PR creata: $prUrl"
Ok "Vai sulla PR e attendi che 'CI / test (pull_request)' giri (verde)."
