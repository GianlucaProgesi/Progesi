# Strumenti PowerShell per Progesi (CI & Diagnostics)

Sono stati generati due script PowerShell pronti all'uso:

- **Invoke-ProgesiWorkflow.ps1** — lancia un workflow (per nome o file) e attende il risultato; produce un **report JSON/Markdown** e scarica gli **artifacts** opzionalmente.
- **Get-ProgesiDiagnostics.ps1** — produce un bundle diagnostico del repository (workflows, ultimi run, dettaglio dell'ultimo run fallito) con **report Markdown/JSON**.

## Requisiti
- [GitHub CLI](https://cli.github.com/) (`gh`) installato e autenticato:
  ```powershell
  gh auth status
  gh auth login -h github.com -s repo,workflow,read:packages,write:packages
  ```

## Uso rapido
Esegui gli script dalla root del repository locale (così deducono automaticamente `OWNER/REPO` e il branch corrente).

```powershell
# Lancia CI e attendi esito
pwsh -File ./Invoke-ProgesiWorkflow.ps1 -Workflow 'ci.yml' -DownloadArtifacts

# Lancia Release su main
pwsh -File ./Invoke-ProgesiWorkflow.ps1 -Workflow 'release.yml' -Ref 'main'

# Bundle diagnostico (ultimi 10 run, 3 per workflow), con artifacts del fallito
pwsh -File ./Get-ProgesiDiagnostics.ps1 -PerWorkflow 3 -DownloadArtifacts
```

## Note
- Puoi passare `-Repo 'GianlucaProgesi/Progesi'` se non lanci dalla repo.
- Per i workflow puoi usare **il nome** (es. `CI`, `Release`) oppure **il file** (`ci.yml`, `release.yml`).
- I report vengono salvati in cartelle con timestamp nella directory corrente.
