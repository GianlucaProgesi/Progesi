<#
.SYNOPSIS
  Estrae le percentuali di coverage (Line/Branch/Method) dall’ultimo run completato
  di un workflow GitHub Actions, scaricando l’artifact "CoverageReport" e leggendo Summary.txt.

.PARAMETER Token
  GitHub token con scope read:actions (PAT) o GITHUB_TOKEN. Se omesso usa $env:GITHUB_TOKEN.

.PARAMETER Owner
  Owner/organizzazione del repo. Se omesso, viene dedotto da "git remote get-url origin".

.PARAMETER Repo
  Nome del repository. Se omesso, viene dedotto da "git remote get-url origin".

.PARAMETER Workflow
  ID o file name del workflow (es. 'ci.yml'). Default: 'ci.yml'.

.PARAMETER Branch
  Branch da cui leggere l’ultimo run completato. Default: 'main'. (Usa il branch corrente se passi '-Branch @()').

.PARAMETER RunId
  Se specificato, salta la ricerca e usa direttamente questo run.

.PARAMETER OutJson
  Se presente, stampa anche un JSON compatto dei risultati.

.EXAMPLE
  pwsh -File tools/Get-CoverageFromGitHub.ps1 -OutJson

.EXAMPLE
  pwsh -File tools/Get-CoverageFromGitHub.ps1 -Owner GianlucaProgesi -Repo Progesi -Workflow ci.yml -Branch main

.EXAMPLE
  # Usa un PAT esplicito
  pwsh -File tools/Get-CoverageFromGitHub.ps1 -Token $env:GH_PAT -OutJson
#>

param(
  [string]$Token,
  [string]$Owner,
  [string]$Repo,
  [string]$Workflow = "ci.yml",
  [object]$Branch = "main",
  [long]$RunId,
  [switch]$OutJson
)

#-------------------- helpers --------------------
function Write-Info($msg){ Write-Host $msg -ForegroundColor Cyan }
function Write-Warn($msg){ Write-Warning $msg }
function Throw-IfEmpty([string]$val,[string]$name){
  if ([string]::IsNullOrWhiteSpace($val)){ throw "Parametro mancante: $name" }
}

function Get-OwnerRepoFromGit {
  try {
    $url = (git remote get-url origin) 2>$null
    if (-not $url){ return $null }
    # supporta sia git@github.com:owner/repo.git che https://github.com/owner/repo.git
    if ($url -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>[^\.]+)'){
      return @{ owner = $Matches.owner; repo = $Matches.repo }
    }
    return $null
  } catch { return $null }
}

function Invoke-GHApi {
  param(
    [string]$Method = "GET",
    [string]$Url,
    [hashtable]$Headers,
    [object]$Body = $null,
    [switch]$AsBytes
  )
  $params = @{
    Method      = $Method
    Uri         = $Url
    Headers     = $Headers
    ErrorAction = 'Stop'
  }
  if ($Body){ $params.Body = ($Body | ConvertTo-Json -Depth 6) }
  if ($AsBytes){ return Invoke-WebRequest @params -UseBasicParsing }
  else         { return Invoke-RestMethod  @params }
}

#-------------------- prep --------------------
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

if (-not $Token){ $Token = $env:GITHUB_TOKEN }
Throw-IfEmpty $Token "Token (o env:GITHUB_TOKEN)"

if (-not $Owner -or -not $Repo) {
  $pair = Get-OwnerRepoFromGit
  if ($pair) { if (-not $Owner){ $Owner = $pair.owner }; if (-not $Repo){ $Repo = $pair.repo } }
}

Throw-IfEmpty $Owner "Owner"
Throw-IfEmpty $Repo  "Repo"

$base = "https://api.github.com"
$stdHeaders = @{
  "Authorization" = "Bearer $Token"
  "Accept"        = "application/vnd.github+json"
  "User-Agent"    = "Progesi-Coverage-Script"
}

#-------------------- trova run --------------------
if (-not $RunId) {
  $branchParam = ""
  if ($Branch -is [string] -and $Branch.Length -gt 0) { $branchParam = "&branch=$Branch" }
  elseif ($Branch -is [array] -or $Branch -eq $null) {
    # prendi branch corrente da git
    $current = (git rev-parse --abbrev-ref HEAD) 2>$null
    if ($current){ $branchParam = "&branch=$current" }
  }

  $runsUrl = "$base/repos/$Owner/$Repo/actions/workflows/$Workflow/runs?status=completed$branchParam&per_page=1"
  Write-Info "→ Cerco ultimo run completato: $Owner/$Repo • workflow=$Workflow $branchParam"
  $runs = Invoke-GHApi -Url $runsUrl -Headers $stdHeaders
  if (-not $runs.workflow_runs -or $runs.workflow_runs.Count -eq 0) {
    throw "Nessun run completato trovato per il workflow '$Workflow'."
  }
  $run = $runs.workflow_runs[0]
  $RunId = [long]$run.id
  $runHtmlUrl = $run.html_url
  $runHeadSha = $run.head_sha
  $runBranch  = $run.head_branch
} else {
  $run = Invoke-GHApi -Url "$base/repos/$Owner/$Repo/actions/runs/$RunId" -Headers $stdHeaders
  $runHtmlUrl = $run.html_url
  $runHeadSha = $run.head_sha
  $runBranch  = $run.head_branch
}

Write-Info "✔ Run selezionato: id=$RunId branch=$runBranch sha=$runHeadSha"
#-------------------- lista artifacts --------------------
$arts = Invoke-GHApi -Url "$base/repos/$Owner/$Repo/actions/runs/$RunId/artifacts?per_page=100" -Headers $stdHeaders
if (-not $arts.artifacts -or $arts.artifacts.Count -eq 0) { throw "Nessun artifact trovato per il run $RunId." }

# Preferisci "CoverageReport", altrimenti il primo che contiene "coverage"
$artifact = $arts.artifacts | Where-Object { $_.name -eq "CoverageReport" } | Select-Object -First 1
if (-not $artifact) {
  $artifact = $arts.artifacts | Where-Object { $_.name -match 'coverage' } | Select-Object -First 1
}
if (-not $artifact) { throw "Artifact 'CoverageReport' non trovato tra: " + ($arts.artifacts.name -join ', ') }

Write-Info "✔ Artifact: '$($artifact.name)' (id=$($artifact.id))"

#-------------------- scarica artifact zip --------------------
$tmpRoot = [System.IO.Path]::GetTempPath()
$zipPath = Join-Path $tmpRoot ("coverage_" + $artifact.id + ".zip")
$outDir  = Join-Path $tmpRoot ("coverage_" + $artifact.id)

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
if (Test-Path $outDir)  { Remove-Item $outDir -Recurse -Force }

$zipResp = Invoke-GHApi -Url "$base/repos/$Owner/$Repo/actions/artifacts/$($artifact.id)/zip" -Headers $stdHeaders -AsBytes
$zipResp.Content | Set-Content -Path $zipPath -Encoding Byte

Expand-Archive -Path $zipPath -DestinationPath $outDir -Force

# Cerca Summary.txt ovunque dentro l’estrazione
$summaryPath = Get-ChildItem -Recurse -Path $outDir -Filter "Summary.txt" | Select-Object -First 1
if (-not $summaryPath) { throw "Summary.txt non trovato dentro l’artifact." }

$text = Get-Content -Raw $summaryPath.FullName
function Extract-Percent([string]$pattern) {
  if ($text -match $pattern) { return [double]$Matches[1] } else { return $null }
}

$line   = Extract-Percent 'Line coverage:\s*([\d\.]+)%'
$branch = Extract-Percent 'Branch coverage:\s*([\d\.]+)%'
$method = Extract-Percent 'Method coverage:\s*([\d\.]+)%'

if ($null -eq $line -or $null -eq $branch -or $null -eq $method) {
  throw "Impossibile estrarre le percentuali dal Summary.txt"
}

#-------------------- output --------------------
$result = [pscustomobject]@{
  Owner        = $Owner
  Repo         = $Repo
  Workflow     = $Workflow
  Branch       = $runBranch
  RunId        = $RunId
  RunUrl       = $runHtmlUrl
  Commit       = $runHeadSha
  ArtifactId   = $artifact.id
  ArtifactName = $artifact.name
  Line         = [math]::Round($line,1)
  BranchPct    = [math]::Round($branch,1)
  Method       = [math]::Round($method,1)
  SummaryPath  = $summaryPath.FullName
}

Write-Host ""
Write-Host "### Coverage (dal run $RunId)" -ForegroundColor Green
Write-Host ("Line:   {0:N1}%" -f $result.Line)
Write-Host ("Branch: {0:N1}%" -f $result.BranchPct)
Write-Host ("Method: {0:N1}%" -f $result.Method)
Write-Host ("Run:    {0}" -f $result.RunUrl) -ForegroundColor DarkGray
Write-Host ""

if ($OutJson) {
  $result | ConvertTo-Json -Depth 4
} else {
  # restituisce comunque l’oggetto alla pipeline
  $result
}
