# Progesi Tools

## Invoke-Workflow.ps1
Lancia un workflow GitHub Actions e salva un report locale.
```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\Invoke-Workflow.ps1 -Workflow 'ci.yml' -DownloadArtifacts
## Run-TestsWithCoverage.ps1
- **Cosa**: esegue build+test con coverage Cobertura e genera HTML.
- **Come**: pwsh -File tools/Run-TestsWithCoverage.ps1
- **Quando**: prima di aprire PR o taggare una release.

## Verify-Release.ps1
- **Cosa**: verifica i pacchetti *.nupkg per una versione e (opzionale) pubblica su GitHub Packages.
- **Come**:
  - Dry-run: pwsh -File tools/Verify-Release.ps1 -Version 1.0.0
  - Pack se mancano: ... -Version 1.0.0 -PackIfMissing
  - Publish GPR: ... -Version 1.0.0 -PublishGpr -GprToken <PAT|GITHUB_TOKEN>
- **Quando**: prima/dopo il workflow di release per diagnosticare errori di publish.
