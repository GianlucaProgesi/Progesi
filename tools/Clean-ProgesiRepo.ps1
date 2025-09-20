[CmdletBinding()]
param(
  [switch]$Apply,        # elimina davvero
  [switch]$VerboseScan   # elenca YAML/sospetti
)

$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
Write-Host "Progesi clean @ $root" -ForegroundColor Cyan
Write-Host ("Mode: {0}" -f ($(if($Apply){"APPLY"}else{"DRY-RUN"}))) -ForegroundColor Yellow
Write-Host ""

# raccolta target
$candidates = New-Object System.Collections.Generic.List[string]

function AddIfExists([string[]]$paths) {
  foreach ($p in $paths) {
    if ([string]::IsNullOrWhiteSpace($p)) { continue }
    try {
      if (Test-Path -LiteralPath $p) {
        foreach ($rp in (Resolve-Path -LiteralPath $p -ErrorAction SilentlyContinue)) {
          if ($rp -and $rp.Path) { [void]$candidates.Add($rp.Path) }
        }
      }
    } catch {
      Write-Warning ("Skipping '{0}': {1}" -f $p, $_.Exception.Message)
    }
  }
}

# cartelle temp/artefatti note
AddIfExists @(
  (Join-Path $root 'nupkg'),
  (Join-Path $root 'TestResults'),
  (Join-Path $root 'out\coverage\TestResults')
)

# run-report-*/artifacts
if (Test-Path -LiteralPath $root) {
  Get-ChildItem -LiteralPath $root -Directory -Filter 'run-report-*' -ErrorAction SilentlyContinue |
    ForEach-Object { AddIfExists (Join-Path $_.FullName 'artifacts') }
}

# bin/obj/TestResults sotto src/ e tests/
foreach ($base in @((Join-Path $root 'src'), (Join-Path $root 'tests'))) {
  if (Test-Path -LiteralPath $base) {
    Get-ChildItem -LiteralPath $base -Directory -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
      if ($_.Name -in @('bin','obj','TestResults')) { AddIfExists $_.FullName }
    }
  }
}

$uniq = $candidates | Where-Object { $_ -and -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique
Write-Host "== Candidate removals ==" -ForegroundColor Green
if ($uniq.Count) { $uniq | ForEach-Object { Write-Host $_ } } else { Write-Host "(none)" }

if ($VerboseScan) {
  Write-Host ""
  Write-Host "== Suspicious workflow files (manual review) ==" -ForegroundColor DarkYellow
  foreach ($wf in @('.github\workflows\labeler.yml','.github\workflows\build-test-coverage.yml')) {
    $full = Join-Path $root $wf
    if (Test-Path -LiteralPath $full) { Write-Host $full }
  }
  Write-Host "`n== YAML files under .github/workflows ==" -ForegroundColor DarkCyan
  $wfDir = Join-Path $root '.github\workflows'
  if (Test-Path -LiteralPath $wfDir) {
    Get-ChildItem -LiteralPath $wfDir -File -Filter *.yml | ForEach-Object { Write-Host $_.FullName }
  } else { Write-Host "(folder missing)" }
}

if ($Apply) {
  Write-Host "`nRemoving..." -ForegroundColor Yellow
  foreach ($p in $uniq) {
    try {
      Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction Stop
    } catch {
      Write-Warning ("Failed to remove {0}: {1}" -f $p, $_.Exception.Message)
    }
  }
  Write-Host "Done."
} else {
  Write-Host "`nDRY-RUN complete. Re-run with -Apply to actually delete."
}
