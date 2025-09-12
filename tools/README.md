# Progesi Tools

## Invoke-Workflow.ps1
Lancia un workflow GitHub Actions e salva un report locale.
```powershell
pwsh -ExecutionPolicy Bypass -File .\tools\Invoke-Workflow.ps1 -Workflow 'ci.yml' -DownloadArtifacts
