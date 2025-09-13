param([string]$Ref)

if (-not $Ref -or $Ref -eq '') {
  $Ref = (git rev-parse --abbrev-ref HEAD).Trim()
}

Write-Host "Trigger CI on '$Ref'..."
gh workflow run ci.yml --ref $Ref | Out-Host

# Poll dell'ultima run del workflow su quel branch
$runId = $null
for ($i=0; $i -lt 20 -and -not $runId; $i++) {
  Start-Sleep -Seconds 3
  $runId = gh run list --workflow "ci.yml" --branch $Ref --json databaseId -q '.[0].databaseId' 2>$null
}

if ($runId) {
  gh run watch $runId --exit-status
  gh run view  $runId
} else {
  Write-Warning "Non trovo la run appena creata; apri GitHub → Actions per verificarla."
}
