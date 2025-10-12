Param(
  [string]$Repo = "GianlucaProgesi/Progesi",
  [string]$ProtectBranch = "main",
  [string[]]$KeepBranches = @("main","release/"),
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
function Info($m){ Write-Host $m -ForegroundColor Cyan }
function Ok($m){ Write-Host $m -ForegroundColor Green }
function Warn($m){ Write-Warning $m }

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "Installare GitHub CLI: https://cli.github.com/" }
gh auth status 1>$null 2>$null; if ($LASTEXITCODE -ne 0) { gh auth login -w | Out-Null }

# A) Branch protection (minimo necessario)
Info "Imposto branch protection su $ProtectBranch…"
# Required status check = solo CI / test (pull_request); strict=false per evitare blocchi inutili
gh api -X PATCH "repos/$Repo/branches/$ProtectBranch/protection/required_status_checks" `
  -H "Accept: application/vnd.github+json" --input -  <<< '{ "strict": false }' 2>$null | Out-Null

# reset contexts e imposta CI PR
try {
  $cur = gh api -H "Accept: application/vnd.github+json" "repos/$Repo/branches/$ProtectBranch/protection/required_status_checks" | ConvertFrom-Json
  if ($cur.contexts) {
    foreach($c in $cur.contexts){ gh api -X DELETE "repos/$Repo/branches/$ProtectBranch/protection/required_status_checks/contexts" -F "contexts[]=$c" | Out-Null }
  }
} catch {}
gh api -X POST -H "Accept: application/vnd.github+json" `
  "repos/$Repo/branches/$ProtectBranch/protection/required_status_checks/contexts" `
  -F "contexts[]=CI / test (pull_request)" | Out-Null

# Linear history
gh api -X PUT "repos/$Repo/branches/$ProtectBranch/protection/required_linear_history" 2>$null | Out-Null
# Repo-wide: auto-merge + delete-branch-on-merge + squash allowed
gh api -X PATCH "repos/$Repo" -H "Accept: application/vnd.github+json" `
  -f allow_auto_merge=true -f delete_branch_on_merge=true -f allow_squash_merge=true -f allow_merge_commit=false | Out-Null

Ok "Branch protection impostata."

# B) Disabilita workflow non usati (se presenti)
Info "Disabilito workflows non necessari (se presenti)…"
gh workflow disable "PR Labeler" 2>$null | Out-Null
gh workflow disable "CodeQL" 2>$null | Out-Null

# C) Pulisci rami remoti
Info "Pulizia rami remoti (whitelist: $($KeepBranches -join ', '))…"
$remoteBranches = (git ls-remote --heads https://github.com/$Repo.git | ForEach-Object {
  ($_ -split "`t")[1] -replace '^refs/heads/',''
})
foreach($rb in $remoteBranches){
  $keep = $false
  foreach($k in $KeepBranches){
    if ($rb -eq $k -or ($k.EndsWith("/") -and $rb.StartsWith($k))) { $keep=$true; break }
  }
  if (-not $keep) {
    if ($DryRun) { Warn ("[DRYRUN] delete remote branch: {0}" -f $rb) }
    else {
      Info ("delete remote branch: {0}" -f $rb)
      git push https://github.com/$Repo.git :$rb | Out-Null
    }
  }
}
Ok "Pulizia GitHub completata."
