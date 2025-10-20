param(
  [string]$Branch = 's1-5_dataex-e2e',
  [string]$Title  = 'io: S1-5 DataEx Excel (export + import) - RHINO-only',
  [string]$Body   = @'
DataEx: ExportExcel + ImportExcel (repo Rhino <-> xlsx).
Coerente con dedupe Var/Meta; summary umani; indici VarHash/VarStrictHash/MetaContentHash.
Build 0 warnings; Tests 107/107.
'@,
  [switch]$AllowEmptyCommit,
  [switch]$BuildAndTest
)

$ErrorActionPreference = 'Stop'

function Invoke-Git {
  param([Parameter(Mandatory=$true)][string[]]$Args)
  & git @Args
  if ($LASTEXITCODE -ne 0) { throw "git $($Args -join ' ') -> exit $LASTEXITCODE" }
}

# 0) repo e remote
Invoke-Git @('rev-parse','--is-inside-work-tree') | Out-Null
$remotes = (git remote) -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if (-not ($remotes -contains 'origin')) {
  throw 'Remote "origin" non trovato. Aggiungi: git remote add origin https://YOUR_REMOTE_URL'
}

# 1) fetch/prune
Invoke-Git @('fetch','--all','--prune') | Out-Null

# 2) crea/switch branch
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  & git rev-parse --verify --quiet "refs/heads/$Branch" *> $null
  if ($LASTEXITCODE -eq 0) { Invoke-Git @('switch', $Branch) } else { Invoke-Git @('switch','-c', $Branch) }
}
$now = (git rev-parse --abbrev-ref HEAD).Trim()
if ($now -ne $Branch) { throw "Non sono su $Branch (sono su $now)" }

# 3) build+test opzionali
if ($BuildAndTest) {
  & dotnet restore
  if ($LASTEXITCODE -ne 0) { throw 'dotnet restore fallito' }
  & dotnet build -c Release
  if ($LASTEXITCODE -ne 0) { throw 'dotnet build fallito' }
  & dotnet test  -c Release --no-build
  if ($LASTEXITCODE -ne 0) { throw 'dotnet test fallito' }
}

# 4) commit (solo se ci sono modifiche; opzionale empty commit)
$porcelain = (git status --porcelain)
if ($porcelain) {
  Invoke-Git @('add','-A')
  Invoke-Git @('commit','-m', $Title, '-m', $Body, '--no-verify')
} elseif ($AllowEmptyCommit) {
  Invoke-Git @('commit','--allow-empty','-m', $Title, '-m', $Body, '--no-verify')
} else {
  Write-Host 'Nessuna modifica locale da committare.'
}

# 5) push – se non c'e upstream lo crea
try {
  Invoke-Git @('push')
} catch {
  Invoke-Git @('push','-u','origin', $Branch)
}

Write-Host ''
Write-Host ("[OK] Push su origin/{0} completato" -f $Branch)
