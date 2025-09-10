[CmdletBinding()]
param(
  [string]$OutputPath = './CHANGELOG.md',
  [string]$LatestPath = './.changelog-LATEST.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Fallback difensivi in caso di binding nullo
if (-not $OutputPath -or [string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = './CHANGELOG.md' }
if (-not $LatestPath -or [string]::IsNullOrWhiteSpace($LatestPath)) { $LatestPath = './.changelog-LATEST.md' }

function Write-FileUtf8 {
  param([Parameter(Mandatory=$true)][string]$TargetPath,
        [Parameter(Mandatory=$true)][string]$Text)
  $dir = Split-Path -Parent $TargetPath
  if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
  $enc = New-Object System.Text.UTF8Encoding($false) # no BOM
  [System.IO.File]::WriteAllText($TargetPath, $Text, $enc)
}

# ---- raccogli commit da ultimo tag ----
$lastTag = (git tag --list 'v*' --sort=-v:refname | Select-Object -First 1)
if (-not $lastTag) { $lastTag = 'v0.0.0' }

$range   = if ($lastTag -eq 'v0.0.0') { '' } else { "$lastTag..HEAD" }
$commits = git log --no-merges --pretty=format:'%H|%s' $range

if ([string]::IsNullOrWhiteSpace($commits)) {
  Write-Host "Nessun commit da includere nel changelog." -ForegroundColor Yellow
  # Anche se vuoto, assicuriamo che i file esistano (vuoti) per evitare errori downstream
  Write-FileUtf8 -TargetPath $LatestPath -Text ""
  if (-not (Test-Path $OutputPath)) { Write-FileUtf8 -TargetPath $OutputPath -Text "" }
  exit 0
}

$features=@(); $fixes=@(); $others=@(); $breaking=@()
foreach ($c in ($commits -split "`n")) {
  $parts = $c -split '\|',2
  if ($parts.Count -lt 2) { continue }
  $subj = $parts[1]
  if ($subj -match 'BREAKING CHANGE' -or $subj -match '^[a-z]+(\(.+\))?!:') { $breaking += $subj; continue }
  if ($subj -match '^feat(\(.+\))?:') { $features += $subj; continue }
  if ($subj -match '^fix(\(.+\))?:')  { $fixes += $subj; continue }
  $others += $subj
}

function Get-NextTag([string]$last,[string[]]$feats,[string[]]$brks) {
  if ($last -notmatch '^v(\d+)\.(\d+)\.(\d+)$') { $last = 'v0.0.0' }
  $M=[int]$Matches[1]; $m=[int]$Matches[2]; $p=[int]$Matches[3]
  if ($brks.Count -gt 0) { $M++; $m=0; $p=0 }
  elseif ($feats.Count -gt 0) { $m++; $p=0 }
  else { $p++ }
  return "v$M.$m.$p"
}

$newTag = Get-NextTag $lastTag $features $breaking
$today  = (Get-Date).ToString('yyyy-MM-dd')

function Section([string]$title,[string[]]$items) {
  if (-not $items -or $items.Count -eq 0) { return @() }
  $o=@(); $o+="# $title"; foreach ($i in $items) { $o+=("- " + $i) }; $o+=""
  return ,$o
}

$latestSectionLines = @()
$latestSectionLines += "## $newTag ($today)"
$latestSectionLines += ""
$latestSectionLines += Section "Breaking" $breaking
$latestSectionLines += Section "Features" $features
$latestSectionLines += Section "Fixes"    $fixes
$latestSectionLines += Section "Other"    $others
$latestSection = ($latestSectionLines -join "`n").TrimEnd() + "`n"

# Scrivi .changelog-LATEST.md
Write-FileUtf8 -TargetPath $LatestPath -Text $latestSection
Write-Host "✓ Scritto $LatestPath"

# Prepend in CHANGELOG.md
if (Test-Path $OutputPath) {
  $prev = [System.IO.File]::ReadAllText($OutputPath)
  $new  = $latestSection + "`n" + $prev.TrimStart()
} else {
  $new = $latestSection
}
Write-FileUtf8 -TargetPath $OutputPath -Text $new
Write-Host "✓ Aggiornato $OutputPath"
