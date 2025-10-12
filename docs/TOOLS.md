# TOOLING – scripts

## tools\Repo-Clean-Local.ps1
- **Cosa fa**: trash/delete di cartelle/diagnostici, submodule cleanup, bin/obj; crea branch di cleanup; build/test opzionali.
- **Come si usa**:
  - move-to-trash: `.\tools\Repo-Clean-Local.ps1`
  - delete reale:  `.\tools\Repo-Clean-Local.ps1 -Delete`

## tools\Repo-Clean-GitHub.ps1
- **Cosa fa**: imposta branch protection minima; disabilita workflow inutili; elimina rami remoti fuori whitelist.
- **Come si usa**:
  - prova: `.\tools\Repo-Clean-GitHub.ps1 -DryRun`
  - esegui: `.\tools\Repo-Clean-GitHub.ps1`

## tools\Commit-Push-PR.ps1
- **Cosa fa**: commit → push → PR → (opzionale) avvio CI.
- **Uso**: `.\tools\Commit-Push-PR.ps1 -CommitMessage "..." -RunCI`
