# Progesi – Manutenzione CI/CD (no-surprises)

## Workflow
- **CI**: parte su Pull Request (tutti i branch) e su push a `main`.
  - Required check: **CI / test (pull_request)**.
- **build-and-pack**: manuale (`Actions → build-and-pack → Run`) o su tag `v*`.
- **smoke**: manuale con input **ref** (branch da testare).

## Comandi rapidi (CLI)
```powershell
# CI su PR branch
gh workflow run CI --ref <branch>

# build-and-pack manuale (su main o branch)
gh workflow run build-and-pack --ref <branch>

# release: crea tag e lascia che build-and-pack pubblichi lo zip
git tag v0.3.0
git push origin v0.3.0
