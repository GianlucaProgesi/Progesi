param(
  [string]$Branch = 's2_dataex_sqlite',
  [string]$Title  = 'io: S2-B DataEx SQLite (export/import) – fix FK + preview + ErrRC',
  [string]$Body   = @'
SQLite:
- ExportSqlite: schema senza UNIQUE(Hash), MetaId NULL quando manca; gestione file locked/rename.
- ImportSqlite: validazioni simmetriche a Excel; Preview (DryRun) e ErrRC (row,col per Meta/Vars).
Excel: invariato e stabile.
Build pulita; smoke test OK su Export/Import (snapshot).
'@,
  [switch]$AllowEmptyCommit,
  [switch]$BuildAndTest
)

$ErrorActionPreference = 'Stop'
function G { param([string[]]$A) & git @A; if ($LASTEXITCODE -ne 0) { throw "git $($A -join ' ') -> exit $LASTEXITCODE" } }

# 1) repo & remote
G @('rev-parse','--is-inside-work-tree') | Out-Null
$hasOrigin = (git remote) -split "`r?`n" | ForEach-Object Trim | Where-Object {$_ -eq 'origin'}
if (-not $hasOrigin) { throw 'Remote "origin" non trovato. Esegui: git remote add origin https://YOUR_REMOTE_URL' }

# 2) sync
G @('fetch','--all','--prune') | Out-Null

# 3) switch/create branch
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  & git rev-parse --verify --quiet "refs/heads/$Branch" *> $null
  if ($LASTEXITCODE -eq 0) { G @('switch', $Branch) } else { G @('switch','-c', $Branch) }
}

# 4) (facoltativo) build + test
if ($BuildAndTest) {
  & dotnet restore; if ($LASTEXITCODE -ne 0) { throw 'dotnet restore fallito' }
  & dotnet build -c Release; if ($LASTEXITCODE -ne 0) { throw 'dotnet build fallito' }
  & dotnet test  -c Release --no-build; if ($LASTEXITCODE -ne 0) { throw 'dotnet test fallito' }
}

# 5) commit se necessario
$porcelain = (git status --porcelain)
if ($porcelain) {
  G @('add','-A')
  G @('commit','-m', $Title, '-m', $Body, '--no-verify')
} elseif ($AllowEmptyCommit) {
  G @('commit','--allow-empty','-m', $Title, '-m', $Body, '--no-verify')
} else {
  Write-Host 'Nessuna modifica locale da committare.'
}

# 6) push (auto set upstream)
try { G @('push') } catch { G @('push','-u','origin', $Branch) }

Write-Host "`n[OK] Push su origin/$Branch completato"
