param(
  [string]$Branch = 's2-c2_pure-sqlite-gh',
  [string]$Title  = 'S2-C/2: GH plugin -> pure SQLite (remove EF; map ExportEf/ImportEf to SQLite)',
  [switch]$RunTests,
  [switch]$AllowEmptyCommit
)

$ErrorActionPreference = 'Stop'

function G {
  param([string[]]$A)
  & git @A
  if ($LASTEXITCODE -ne 0) { throw ("git " + ($A -join ' ') + " -> exit " + $LASTEXITCODE) }
}

# -- verifica repo --
G @('rev-parse','--is-inside-work-tree') | Out-Null
G @('fetch','--all','--prune') | Out-Null

# -- solution --
$sln = Get-ChildItem -Path . -Filter *.sln | Select-Object -First 1
if (-not $sln) { throw "Solution .sln non trovata nella root." }

# -- crea/switch branch --
& git rev-parse --verify --quiet ("refs/heads/" + $Branch) *> $null
if ($LASTEXITCODE -eq 0) { G @('switch', $Branch) } else { G @('switch','-c', $Branch) }

# -- build/test facoltativi --
if ($RunTests) {
  & dotnet clean $sln.FullName;       if ($LASTEXITCODE -ne 0) { throw 'dotnet clean fallita' }
  & dotnet build $sln.FullName -c Release;  if ($LASTEXITCODE -ne 0) { throw 'dotnet build fallita' }
  & dotnet test  $sln.FullName -c Release --no-build; if ($LASTEXITCODE -ne 0) { throw 'dotnet test falliti' }
}

# -- commit/push --
$porcelain = (git status --porcelain)
if ($porcelain) {
  G @('add','-A')
  G @('commit','-m', $Title)
} elseif ($AllowEmptyCommit) {
  G @('commit','--allow-empty','-m', $Title)
} else {
  Write-Host 'Nessuna modifica locale da committare.'
}

try { G @('push') } catch { G @('push','-u','origin',$Branch) }

Write-Host "`n[OK] Push completato su origin/$Branch"
