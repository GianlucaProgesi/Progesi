<#
.SYNOPSIS
  Elenca per ogni classe/file le righe scoperte (hits=0) dal report Cobertura.
  Se 'CoverageReport/Cobertura.xml' non esiste:
   - prova a generarlo dai TestResults con reportgenerator (stessi filtri del CI);
   - in alternativa, scarica l'artifact "CoverageReport" dall'ultimo run (gh CLI).

.PARAMETER CoberturaPath
  Percorso Cobertura.xml (default: CoverageReport/Cobertura.xml)

.PARAMETER ClassFilter
  Regex per filtrare class/file (es: "ProgesiSnip|ProgesiMetadata")

.PARAMETER Top
  Mostra solo i primi N elementi con più righe scoperte (default: 50)

.PARAMETER TryRebuild
  Se manca Cobertura.xml, prova a ricostruirlo dai TestResults (default: true)

.PARAMETER TryDownload
  Se manca tutto, prova a scaricare l'artifact CoverageReport con 'gh' (default: true)

.PARAMETER WorkflowFile
  Path del workflow per 'gh run list/download' (default: .github/workflows/ci.yml)

.PARAMETER Branch
  Branch da cui prendere l'ultimo run (default: branch corrente)

.EXAMPLE
  pwsh -File tools/Get-UncoveredCoverage.ps1

.EXAMPLE
  pwsh -File tools/Get-UncoveredCoverage.ps1 -ClassFilter ProgesiSnip -Top 20
#>

param(
  [string]$CoberturaPath = "CoverageReport/Cobertura.xml",
  [string]$ClassFilter,
  [int]$Top = 50,
  [bool]$TryRebuild = $true,
  [bool]$TryDownload = $true,
  [string]$WorkflowFile = ".github/workflows/ci.yml",
  [string]$Branch
)

function Write-Info($m){ Write-Host $m -ForegroundColor Cyan }
function Write-Note($m){ Write-Host $m -ForegroundColor DarkGray }
function Ensure-ReportGenerator {
  if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    Write-Info "→ Installo reportgenerator (global tool)…"
    dotnet tool update --global dotnet-reportgenerator-globaltool | Out-Null
    if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
      throw "reportgenerator non disponibile e installazione fallita."
    }
  }
}

function Rebuild-CoverageReport {
  # cerca i risultati Cobertura di test
  $reports = Get-ChildItem -Recurse -Path "TestResults" -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
    ForEach-Object { $_.FullName }
  if (-not $reports -or $reports.Count -eq 0) {
    Write-Note "Nessun TestResults/**/coverage.cobertura.xml trovato."
    return $false
  }
  Ensure-ReportGenerator
  $reportsArg = ($reports -join ";")
  Write-Info "→ Rigenero CoverageReport dai TestResults…"
  reportgenerator `
    -reports:$reportsArg `
    -targetdir:"CoverageReport" `
    -reporttypes:"Html;TextSummary;Cobertura" `
    -assemblyfilters:"+ProgesiCore" `
    -classfilters:"-*Logger*;-*LegacyExtensions*;-*TraceLogger*" | Out-Null

  return (Test-Path "CoverageReport/Cobertura.xml")
}

function Download-CoverageArtifact {
  if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Note "gh CLI non disponibile: skip download artifact."
    return $false
  }
  if (-not $Branch) {
    $Branch = (git rev-parse --abbrev-ref HEAD) 2>$null
    if (-not $Branch) { $Branch = "main" }
  }
  Write-Info "→ Scarico artifact 'CoverageReport' dall’ultimo run su branch '$Branch'…"
  $runId = gh run list --workflow "$WorkflowFile" --branch "$Branch" -L 1 --json databaseId --jq ".[0].databaseId" 2>$null
  if (-not $runId) {
    Write-Note "Nessun run trovato per $WorkflowFile su $Branch."
    return $false
  }
  # scarica solo l'artifact CoverageReport nella cartella ./CoverageReport
  gh run download $runId -n CoverageReport -D CoverageReport 2>$null | Out-Null
  return (Test-Path "CoverageReport/Cobertura.xml")
}

# --------- ensure Cobertura.xml ----------
if (-not (Test-Path $CoberturaPath)) {
  Write-Note "Cobertura non trovato: $CoberturaPath"
  $ok = $false
  if ($TryRebuild) { $ok = Rebuild-CoverageReport }
  if (-not $ok -and $TryDownload) { $ok = Download-CoverageArtifact }
  if (-not $ok) {
    throw "Impossibile ottenere 'CoverageReport/Cobertura.xml'. Esegui i test, oppure scarica l'artifact del run CI."
  }
}

# --------- parse Cobertura ----------
[xml]$xml = Get-Content -Raw $CoberturaPath

function Join-Ranges([int[]]$nums){
  if (-not $nums -or $nums.Count -eq 0) { return "" }
  $nums = $nums | Sort-Object
  $ranges = @()
  $start = $nums[0]; $prev = $nums[0]
  for ($i=1; $i -lt $nums.Count; $i++){
    if ($nums[$i] -eq $prev + 1) { $prev = $nums[$i]; continue }
    $ranges += ($(if ($start -eq $prev) { "$start" } else { "$start-$prev" }))
    $start = $nums[$i]; $prev = $nums[$i]
  }
  $ranges += ($(if ($start -eq $prev) { "$start" } else { "$start-$prev" }))
  return ($ranges -join ", ")
}

$rows = @()
$classes = $xml.coverage.packages.package.classes.class
foreach ($c in $classes) {
  $clsName = [string]$c.name
  $file = [string]$c.filename
  if ($ClassFilter -and -not ($clsName -match $ClassFilter -or $file -match $ClassFilter)) { continue }

  $lines = @($c.lines.line)
  if (-not $lines) { continue }

  $covered = @()
  $uncovered = @()
  foreach ($ln in $lines) {
    $num  = [int]$ln.number
    $hits = [int]$ln.hits
    if ($hits -gt 0) { $covered += $num } else { $uncovered += $num }
  }

  $lc = if (($covered.Count + $uncovered.Count) -gt 0) {
    [math]::Round(100.0 * $covered.Count / ($covered.Count + $uncovered.Count), 1)
  } else { 0.0 }

  $rows += [pscustomobject]@{
    Class          = $clsName
    File           = $file
    LineCoverage   = $lc
    UncoveredCount = $uncovered.Count
    Uncovered      = Join-Ranges $uncovered
  }
}

$rows = $rows | Sort-Object -Property @{Expression='UncoveredCount';Descending=$true}, @{Expression='Class';Descending=$false}
if ($Top -gt 0) { $rows = $rows | Select-Object -First $Top }

# Output leggibile + oggetto pipeline
$rows | ForEach-Object {
  Write-Host "— $($_.Class)  [$($_.LineCoverage)%]  missing=$($_.UncoveredCount)" -ForegroundColor Cyan
  if ($_.Uncovered) { Write-Host "   lines: $($_.Uncovered)" -ForegroundColor DarkGray }
}
$rows
