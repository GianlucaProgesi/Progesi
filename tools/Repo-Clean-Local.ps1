Param(
  [string]$Solution = "Progesi.sln",
  [switch]$Delete,                 # se assente: move-to-trash (sicuro)
  [string]$TrashRoot = ".trash",   # dove spostare i file in modalità "safe"
  [switch]$NoBuild                 # salta restore/build/test
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }
function Fail($m){ Write-Host $m -ForegroundColor Red }

# ---- helpers
function Get-RepoRoot { (git rev-parse --show-toplevel | Out-String).Trim() }
function To-Rel([string]$abs, [string]$root) {
  $p = Resolve-Path $abs -ErrorAction SilentlyContinue
  if (-not $p) { return $null }
  $full = $p.Path
  if ($full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
    return $full.Substring($root.Length).TrimStart('\','/')
  }
  return $full
}
function Is-Tracked([string]$rel) {
  if ([string]::IsNullOrWhiteSpace($rel)) { return $false }
  & git ls-files --error-unmatch -- "$rel" 1>$null 2>$null
  return ($LASTEXITCODE -eq 0)
}
function Remove-SubmoduleSection([string]$name) {
  # rimuovi la sezione submodule.<name> se esiste in .git/config
  & git config -f .git/config --name-only --get-regexp ("^submodule\."+$name+"\." ) 1>$null 2>$null
  if ($LASTEXITCODE -eq 0) {
    & git config -f .git/config --remove-section ("submodule."+ $name) 1>$null 2>$null
    Warn ("Removed submodule section from .git/config: submodule.{0}" -f $name)
  }
  $modPath = Join-Path ".git\modules" $name
  if (Test-Path $modPath) { Remove-Item -Recurse -Force $modPath }
}

# ---- sanity git
git rev-parse --is-inside-work-tree *> $null
if ($LASTEXITCODE -ne 0) { Fail "Non sei in una repo Git."; exit 1 }

$root = Get-RepoRoot
Set-Location $root

# ---- branch di cleanup
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
$branch = "chore/cleanup-$ts"
git switch -c $branch | Out-Null

# ---- target della root (case-insensitive)
$targets = @(
  ".backups",
  ".githooks",
  "diagnostics-*",
  "run-report-*",
  "dist",                 # attenzione al lower-case
  "Dist",
  "Progesi-mirror.git",
  "Progesi-working"
)

# ---- prepara trash se move-to-trash
if (-not $Delete) {
  $trash = Join-Path $root "$TrashRoot\$ts"
  New-Item -ItemType Directory -Force -Path $trash | Out-Null
  Info ("Trash: {0}" -f $trash)
}

foreach ($pat in $targets) {
  # cerca solo nella root
  $items = Get-ChildItem -Force -Path $root -Filter $pat -ErrorAction SilentlyContinue
  foreach ($it in $items) {
    $abs = $it.FullName
    $rel = To-Rel $abs $root

    if ($Delete) {
      Info ("DELETE {0}" -f $abs)
      Remove-Item -Recurse -Force $abs -ErrorAction SilentlyContinue
      if (Is-Tracked $rel) { git rm -r --cached -- "$rel" 1>$null 2>$null }
    } else {
      $dst = Join-Path $trash $it.Name
      Info ("MOVE {0} -> {1}" -f $abs, $dst)
      Move-Item -Force -Path $abs -Destination $dst
      if (Is-Tracked $rel) { git rm -r --cached -- "$rel" 1>$null 2>$null }
    }

    # Caso speciale: submodule locale 'Progesi-working'
    if ($it.Name -eq "Progesi-working") {
      Remove-SubmoduleSection "Progesi-working"
    }
  }
}

# ---- purge bin/obj + file spazzatura
Get-ChildItem -Recurse -Directory bin,obj -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $root -Filter *.log -File -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $root -Filter *.tmp -File -ErrorAction SilentlyContinue | Remove-Item -Force

# ---- commit
git add -A
git commit -m "chore(cleanup): trash/delete obsolete dirs; submodule cleanup; bin/obj purge" | Out-Null
git push -u origin $branch

# ---- build/test (facoltativi)
if (-not $NoBuild) {
  Info "dotnet restore..."
  dotnet restore $Solution --nologo
  Info "dotnet build (Release)..."
  dotnet build $Solution -c Release --nologo --no-restore
  Info "dotnet test (Release, no-build)..."
  dotnet test $Solution -c Release --no-build --nologo
}

Ok ("Branch di cleanup pronto: {0}" -f $branch)
Write-Host "Apri PR → main (Squash). I 104 test devono restare verdi." -ForegroundColor Yellow
