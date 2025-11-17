param(
  [string]$Version    = 'v0.9.0-beta',
  [string]$SourceBranch = 's2-c2_pure-sqlite-gh',
  [string]$ReleaseBranch = '',
  [string]$ZipName    = 'progesi-gh.zip',
  [switch]$RunTests,
  [switch]$AllowEmptyCommit
)

$ErrorActionPreference = 'Stop'
function G { param([string[]]$A) & git @A; if ($LASTEXITCODE -ne 0) { throw ("git " + ($A -join ' ') + " -> exit " + $LASTEXITCODE) } }

# 0) setup
if ([string]::IsNullOrWhiteSpace($ReleaseBranch)) { $ReleaseBranch = "release/$Version" }
$sln = Get-ChildItem -Path . -Filter *.sln | Select-Object -First 1
if (-not $sln) { throw "Solution .sln non trovata nella root." }

# 1) sync e switch sul ramo sorgente
G @('fetch','--all','--prune') | Out-Null
G @('checkout', $SourceBranch)

# 2) branch di release
& git rev-parse --verify --quiet ("refs/heads/" + $ReleaseBranch) *> $null
if ($LASTEXITCODE -eq 0) { G @('switch', $ReleaseBranch) } else { G @('switch','-c', $ReleaseBranch) }

# 3) build + test
if ($RunTests) {
  & dotnet clean $sln.FullName;                                if ($LASTEXITCODE -ne 0) { throw 'dotnet clean fallita' }
  & dotnet build $sln.FullName -c Release;                     if ($LASTEXITCODE -ne 0) { throw 'dotnet build fallita' }
  & dotnet test  $sln.FullName -c Release --no-build;          if ($LASTEXITCODE -ne 0) { throw 'dotnet test falliti' }
}

# 4) crea pacchetto zip del plugin GH
& "$PSScriptRoot\Make-Releases-Save-Progesi-S2-C2.ps1"
if ($LASTEXITCODE -ne 0) { throw "Packaging fallito" }
$ZipPath = Join-Path $PSScriptRoot $ZipName
if (-not (Test-Path $ZipPath)) { throw "Zip non trovato: $ZipPath" }

# 5) commit (se necessario)
$porcelain = (git status --porcelain)
if ($porcelain) {
  G @('add','-A')
  G @('commit','-m', "Release $Version (beta)")
} elseif ($AllowEmptyCommit) {
  G @('commit','--allow-empty','-m', "Release $Version (beta)")
} else {
  Write-Host 'Nessuna modifica locale da committare.'
}

# 6) push ramo release
try { G @('push') } catch { G @('push','-u','origin', $ReleaseBranch) }

# 7) tag + push tag
G @('tag','-a', $Version, '-m', "Progesi Versione Beta $Version")
G @('push','origin', $Version)

# 8) release GitHub (se hai gh CLI)
$gh = Get-Command gh -ErrorAction SilentlyContinue
if ($gh) {
  $title = "Progesi $Version (Beta)"
  $body  = @"
- GH plugin: pure SQLite (no EF) – DataEx stabile su Export/Import Excel e SQLite
- Strict/Lenient, Preview, ErrRC support
- Docs: README, DEPLOY, TROUBLESHOOTING, CI
"@
  gh release create $Version "$ZipPath" --title "$title" --notes "$body"
  Write-Host "[OK] Release GitHub creata con allegato: $ZipPath"
} else {
  Write-Warning "gh CLI non trovato. Crea la release a mano e allega: $ZipPath"
}

Write-Host "`n[OK] Branch $ReleaseBranch e tag $Version pubblicati."
Write-Host "Apri PR $ReleaseBranch -> main e allega lo zip alla release (se non l'hai già fatto con gh)."
