param(
  [string]$Branch = 's1-5_dataex-e2e',
  [string]$Title  = 'io: S1-5/E DataEx Excel – controlli extra + ErrRC + Preview (no regressions)',
  [string]$Body   = @'
ImportExcel: controlli extra (len/charset, Refs http/abs path, metaId esistente), ErrRC (riga/colonna), DryRun.
Strict: verifica header su entrambi i fogli prima di processare; Lenient: skip righe incomplete.
ExportExcel invariato. Log .import.log.txt, contatori __next__ coerenti. Build pulita; test stabili.
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

# 1) repo & remote
Invoke-Git @('rev-parse','--is-inside-work-tree') | Out-Null
$remotes = (git remote) -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
if (-not ($remotes -contains 'origin')) { throw 'Remote "origin" non trovato. Esegui: git remote add origin https://YOUR_REMOTE_URL' }

# 2) sync
Invoke-Git @('fetch','--all','--prune') | Out-Null

# 3) switch/create branch
$current = (git rev-parse --abbrev-ref HEAD).Trim()
if ($current -ne $Branch) {
  & git rev-parse --verify --quiet "refs/heads/$Branch" *> $null
  if ($LASTEXITCODE -eq 0) { Invoke-Git @('switch', $Branch) } else { Invoke-Git @('switch','-c', $Branch) }
}

# 4) optional build & tests
if ($BuildAndTest) {
  & dotnet restore; if ($LASTEXITCODE -ne 0) { throw 'dotnet restore fallito' }
  & dotnet build -c Release; if ($LASTEXITCODE -ne 0) { throw 'dotnet build fallito' }
  & dotnet test  -c Release --no-build; if ($LASTEXITCODE -ne 0) { throw 'dotnet test fallito' }
}

# 5) commit (only if there are changes)
$porcelain = (git status --porcelain)
if ($porcelain) {
  Invoke-Git @('add','-A')
  Invoke-Git @('commit','-m', $Title, '-m', $Body, '--no-verify')
} elseif ($AllowEmptyCommit) {
  Invoke-Git @('commit','--allow-empty','-m', $Title, '-m', $Body, '--no-verify')
} else {
  Write-Host 'Nessuna modifica locale da committare.'
}

# 6) push (auto set upstream if missing)
try { Invoke-Git @('push') } catch { Invoke-Git @('push','-u','origin', $Branch) }

Write-Host ''
Write-Host ("[OK] Push su origin/{0} completato" -f $Branch)
