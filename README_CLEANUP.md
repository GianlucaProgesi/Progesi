# Progesi – Solution Cleanup & Structure

This repository was restructured to simplify development and deployment for Grasshopper.

## Layout
- `src/` – active production projects (libraries and Grasshopper assemblies)
- `tests/` – unit/integration tests
- `samples/` – example Grasshopper files (`.gh`, `.ghx`)
- `build/` – build scripts, common MSBuild files
- `docs/` – documentation, notes
- `archive_legacy_removed_from_sln/` – old/legacy projects removed from the solution but kept here for reference

A backup of the original `.sln` was saved next to the current one with the suffix `.backup_before_cleanup.sln`.

See also `README_DEPLOY.md` for auto-deploy details.

## Testing
A test project `Progesi.Repositories.Sqlite.Tests` was added under `tests/` with a simple **xUnit smoke test**.

Run tests with:
```
dotnet test
```
