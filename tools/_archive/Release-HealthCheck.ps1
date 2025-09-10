[CmdletBinding()]
param(
  [string]$Workflow = '.github/workflows/release.yml',
  [string]$Props = 'Directory.Build.props',
  [string[]]$ProjectsRoot = @('./src')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$errors = @()

function Fail([string]$msg) { $script:errors += $msg; Write-Host "ERR: $msg" -ForegroundColor Red }
function Ok([string]$msg)   { Write-Host "OK : $msg" -ForegroundColor Green }
function Warn([string]$msg) { Write-Host "WARN: $msg" -ForegroundColor Yellow }

# 1) File base
if (Test-Path $Workflow) { Ok "workflow presente: $Workflow" } else { Fail "workflow mancante: $Workflow" }
if (Test-Path $Props)    { Ok "Directory.Build.props presente" } else { Fail "Directory.Build.props mancante" }

# 2) MinVer in props
if (Test-Path $Props) {
  [xml]$xml = Get-Content -LiteralPath $Props
  $pkg = $xml.Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq 'MinVer' }
  if ($pkg) { Ok "MinVer referenziato (v$($pkg.Version))" } else { Warn "MinVer NON trovato nel props" }
  $pg = $xml.Project.PropertyGroup
  $tagPrefix = $pg.MinVerTagPrefix
  if ($tagPrefix -and $tagPrefix.'#text' -eq 'v') { Ok "MinVerTagPrefix = v" } else { Warn "MinVerTagPrefix non impostato a 'v'" }
}

# 3) Progetti packabili: README + PackageReadmeFile
$csprojs = @()
foreach ($root in $ProjectsRoot) {
  $csprojs += Get-ChildItem -Path $root -Recurse -Filter *.csproj
}
foreach ($cs in $csprojs) {
  [xml]$px = Get-Content -LiteralPath $cs.FullName
  $pg = $px.Project.PropertyGroup | Select-Object -First 1
  if (-not $pg) { continue }
  $isPackable = $false
  if ($pg.IsPackable) { $isPackable = ($pg.IsPackable.'#text' -eq 'true') }
  elseif ($pg.PackageId -or $pg.GeneratePackageOnBuild) { $isPackable = $true }

  if ($isPackable) {
    $prf = $pg.PackageReadmeFile
    if (-not $prf -or $prf.'#text' -ne 'README.md') { Warn "PackageReadmeFile non impostato a README.md in $($cs.Name)" }
    $dir = Split-Path -Parent $cs.FullName
    if (-not (Test-Path (Join-Path $dir 'README.md'))) { Warn "README.md mancante in $dir" } else { Ok "README.md presente in $dir" }
  }
}

# 4) SourceLink
foreach ($cs in $csprojs) {
  [xml]$px = Get-Content -LiteralPath $cs.FullName
  $refs = $px.Project.ItemGroup.PackageReference
  $hasSL = $false
  foreach ($r in $refs) { if ($r.Include -eq 'Microsoft.SourceLink.GitHub') { $hasSL = $true; break } }
  if (-not $hasSL) { Warn "SourceLink non referenziato in $($cs.Name)" }
}

# 5) Tag corrente e working tree
$lastTag = (git tag --list 'v*' --sort=-v:refname | Select-Object -First 1)
if ($lastTag) { Ok "Ultimo tag: $lastTag" } else { Warn "Nessun tag v* trovato" }
$st = git status --porcelain
if ([string]::IsNullOrWhiteSpace($st)) { Ok "Working tree pulito" } else { Warn "Working tree sporco: verifica prima di rilasciare" }

# Esito
if ($errors.Count -gt 0) {
  Write-Host "`nHealth check: FAILED ($($errors.Count) problemi)" -ForegroundColor Red
  exit 1
} else {
  Write-Host "`nHealth check: OK" -ForegroundColor Green
}
