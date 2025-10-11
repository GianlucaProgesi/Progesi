# Progesi â€“ Manutenzione CI/CD (brevissimo)

## Workflow
- **CI**: gira su PR (tutti i branch) e su push a `main`.
  - Required check: `CI / test (pull_request)`.
- **build-and-pack**: solo `workflow_dispatch` (manuale) e/o tag `v*`.
- **smoke**: manuale (`Actions â†’ smoke â†’ Run workflow`) con input **ref** (branch da testare).

## Trigger rapidi
```sh
# CI su main (manuale)
gh workflow run CI --ref main

# build-and-pack su main (manuale)
gh workflow run build-and-pack --ref main

# Release di prova
git tag v0.1.0 && git push origin v0.1.0

