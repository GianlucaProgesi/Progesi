Param(
  [Parameter(Mandatory=$true)][int]$PRNumber,     # es: 51
  [string]$Repo = "GianlucaProgesi/Progesi",
  [int]$Tail = 150
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail "gh non trovato (https://cli.github.com/)"; exit 1 }
gh auth status 1>$null 2>$null; if ($LASTEXITCODE -ne 0) { gh auth login -w | Out-Null }

# 1) branch della PR
$prObj = gh pr view $PRNumber --repo $Repo --json headRefName | ConvertFrom-Json
$branch = $prObj.headRefName
Info ("Branch PR: {0}" -f $branch)

# 2) ultimo run del workflow 'CI' su questo branch
$runsJson = gh run list --workflow "CI" --branch $branch --limit 1 --json databaseId,status,conclusion,headBranch | Out-String
$runs = $runsJson | ConvertFrom-Json
if (-not $runs -or -not $runs[0]) { Warn "Nessun run trovato per CI su $branch."; exit 0 }
$runId = $runs[0].databaseId
Info ("Run CI id: {0} (status={1}, conclusion={2})" -f $runId,$runs[0].status,$runs[0].conclusion)

# 3) elenco jobs del run
Info "Jobs del run:"
gh run view $runId --repo $Repo --job

# 4) id del job 'test' e tail del log
$jobsObj = (gh run view $runId --repo $Repo --json jobs | Out-String) | ConvertFrom-Json
$job = $jobsObj.jobs | Where-Object { $_.name -eq "test" } | Select-Object -First 1
if (-not $job) { Warn ("Job 'test' non trovato. Jobs: {0}" -f ($jobsObj.jobs.name -join ', ')); exit 0 }

Info ("Log job 'test' (ultime {0} righe):" -f $Tail)
# ATTENZIONE: quando usi --job NON devi passare anche il runId
$log = gh run view --repo $Repo --job $job.id --log | Out-String
$lines = $log -split "`r?`n"
$start = [Math]::Max(0, $lines.Length - $Tail)
$lines[$start..($lines.Length-1)] | ForEach-Object { $_ }

Ok "Fine diagnostica."
