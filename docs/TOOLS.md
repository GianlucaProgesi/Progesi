# TOOLING

## tools\Repo-Clean-Local.ps1
- **Cosa**: trash/delete cartelle legacy (.backups, diagnostics-*, Dist, Progesi-working, …), purge bin/obj.
- **Uso**:
  - safe:  .\tools\Repo-Clean-Local.ps1
  - delete:.\tools\Repo-Clean-Local.ps1 -Delete

## tools\Repo-Clean-GitHub.ps1
- **Cosa**: branch protection minima, disabilita workflow inutili, elimina rami remoti fuori whitelist.
- **Uso**:
  - prova: .\tools\Repo-Clean-GitHub.ps1 -DryRun
  - reale:.\tools\Repo-Clean-GitHub.ps1

## tools\Commit-Push-PR.ps1
- **Cosa**: commit → push → PR → (opzionale) avvio CI.
- **Uso**: .\tools\Commit-Push-PR.ps1 -CommitMessage "..." -RunCI
"@ | Out-Null

ni docs\MILESTONES.md -ItemType File -Value @"
# Milestones
- **v0.2.1-stabilized-YYYYMMDD-HHmm** – baseline stabile (104 test verdi), soft-pin Sqlite.
- **v0.2.0-stabilized-…** – recovery Axis + 104 test verdi.