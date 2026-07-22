param(
  [string]$Branch = 's2-c1_ef-integration',
  [string]$Title  = 'io: S2-C/1 EF integration – fallback + external tool wiring (no regressions)',
  [string]$Body   = @'
EF in-proc con fallback stabile a SQLite; integrazione tool esterno Progesi.EF.Tool (build & deploy script);
Export/Import Ef agganciati con fallback e messaggi WHY/TOOL; Excel/SQLite invariati (preview+ErrRC).
'@,
  [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
function G { param([string[]]$A) & git @A; if ($LASTEXITCODE -ne 0) { throw "git $($A -join ' ') -> $LASTEXITCODE" } }

# sync & branch
G @('rev-parse','--is-inside-work-tree') | Out-Null
G @('fetch','--all','--prune') | Out-Null
$cur = (git rev-parse --abbrev-ref HEAD).Trim()
if ($cur -ne $Branch) {
  & git rev-parse --verify --quiet "refs/heads/$Branch" *> $null
  if ($LASTEXITCODE -eq 0) { G @('switch', $Branch) } else { G @('switch','-c', $Branch) }
}

# (facoltativo) build + test
if ($RunTests) {
  & dotnet restore
  if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
  & dotnet build -c Release
  if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
  & dotnet test  -c Release --no-build
  if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed' }
}

# commit push
$porcelain = (git status --porcelain)
if ($porcelain) { G @('add','-o','-A'); G @('commit','-m', $Title, '-m', $Body) }
G @('push','-u','origin', $Branch)
Write-Host "`n[OK] Pushed $Branch → origin"
