[CmdletBinding()]
param(
  [string]$Remote = 'origin',
  [switch]$DryRun,
  [string]$Pre = ''   # opzionale: es. 'beta.1' -> tag vX.Y.Z-beta.1
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Read-File([string]$Path) {
  if (Test-Path -LiteralPath $Path) { return [System.IO.File]::ReadAllText($Path) }
  return ''
}

# 1) Genera/aggiorna changelog (dall'ultimo tag v* a HEAD)
$buildChlog = Join-Path $PSScriptRoot 'Build-Changelog.ps1'
if (-not (Test-Path $buildChlog)) {
  throw "Non trovo tools/Build-Changelog.ps1 nello stesso folder di questo script."
}
& pwsh -File $buildChlog

$latestPath = Join-Path (Get-Location) '.changelog-LATEST.md'
$latestBody = Read-File $latestPath

if ([string]::IsNullOrWhiteSpace($latestBody)) {
  Write-Host 'Nessun contenuto in .changelog-LATEST.md (nessun commit nuovo?). Stop.' -ForegroundColor Yellow
  exit 0
}

# 2) Estrai nuova versione dalla prima riga del changelog (formato: "## vX.Y.Y (YYYY-MM-DD)")
$firstLine = ($latestBody -split "`n" | Select-Object -First 1).Trim()
if ($firstLine -notmatch '^##\s+v(\d+)\.(\d+)\.(\d+)\s+\(') {
  throw "Impossibile estrarre la versione da: '$firstLine'"
}
$baseTag = "v{0}.{1}.{2}" -f $Matches[1], $Matches[2], $Matches[3]
$newTag  = $baseTag
if ($Pre -and -not [string]::IsNullOrWhiteSpace($Pre)) { $newTag = "$baseTag-$Pre" }

Write-Host ("Nuova versione calcolata: {0}" -f $newTag) -ForegroundColor Green

# 3) Committa CHANGELOG se ci sono modifiche
$st = git status --porcelain
if ($st -match '(^|\n)\s*M\s+CHANGELOG\.md' -or $st -match '(^|\n)\?\?\s+CHANGELOG\.md' -or
    $st -match '(^|\n)\s*M\s+\.changelog-LATEST\.md' -or $st -match '(^|\n)\?\?\s+\.changelog-LATEST\.md') {
  git add CHANGELOG.md .changelog-LATEST.md *> $null
  git commit -m ("docs(changelog): update for {0}" -f $newTag)
  Write-Host '✓ Commit changelog eseguito' -ForegroundColor Green
} else {
  Write-Host 'Nessuna modifica al changelog da committare' -ForegroundColor Yellow
}

# 4) Push branch corrente
$currentBranch = (git rev-parse --abbrev-ref HEAD).Trim()
if (-not $DryRun) {
  git push $Remote $currentBranch
  Write-Host ("✓ Push branch {0}" -f $currentBranch) -ForegroundColor Green
} else {
  Write-Host ("[DryRun] Salto push branch {0}" -f $currentBranch) -ForegroundColor Yellow
}

# 5) Crea il tag annotato usando il corpo del changelog
if (-not $DryRun) {
  # Se il tag esiste già, lo rimpiazzo (per comodità)
  if (git tag --list $newTag) { git tag -d $newTag | Out-Null }

  $tmp = [System.IO.Path]::GetTempFileName()
  # Messaggio tag = prima riga con versione + righe successive non vuote
  [System.IO.File]::WriteAllText($tmp, $latestBody, (New-Object System.Text.UTF8Encoding($false)))

  git tag -a $newTag -F $tmp
  git push -f $Remote $newTag
  Remove-Item $tmp -ErrorAction SilentlyContinue
  Write-Host ("✓ Tag {0} creato e pushato" -f $newTag) -ForegroundColor Green
} else {
  Write-Host ("[DryRun] Salto creazione/push tag {0}" -f $newTag) -ForegroundColor Yellow
}

Write-Host "`nFatto. La pipeline di release partirà sul tag $newTag." -ForegroundColor Cyan
