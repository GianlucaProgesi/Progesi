# Progesi – Solution Cleanup & Structure (Historical / Superseded)

> **Status: historical note — superseded.**
> This file is **not** the current cleanup plan and does **not** authorise any cleanup.
> It is retained only as a record of an earlier repository restructuring.

## Current sources of truth

For the current cleanup status, scope and governance, see:

- Notion: **Documentation-only Cleanup Scope — Draft**
- Notion: **Repository Code Cleanup Audit — Draft**
- Notion: **Non-Axis Dead-Code Usage Confirmation — Draft**
- Repository: `docs/repository-cleanup-protocol.md`
- Repository: `CLAUDE.md`

## Current state (reference)

- Protected source checkpoint: **`feat/axis-variable-core` at `376d81e`**.
- Governance branch: **`docs/claude-handover-rules` at `4b481ff`**.
- **Source-code cleanup is not authorised by this file.** Any cleanup requires an approved
  Task Board row, branch, allowed/forbidden files, tests and a rollback plan, per the
  cleanup protocol above.
- **AxisVar remains frozen.** ProgesiVariableCluster recovery is **not authorised**.

## Historical context (no longer accurate)

The notes below describe an earlier restructuring and are kept for history only. They do
**not** reflect the current repository layout and must not be treated as instructions.

- The repository was once described with a layout including `src/`, `tests/`, `samples/`,
  a `docs/` folder, and additional folders that are **no longer present** in the current
  tree (for example a top-level build-scripts folder and a separate legacy-archive folder).
- An earlier `.sln` backup convention and pre-cleanup backup files referenced historically
  here are **no longer part of the current repository** and should be disregarded.
- An earlier test-project name referenced here was approximate; the current test projects
  are defined by the solution and project files (see `Progesi.sln`). Do not rely on the
  historical name in this note.

For the authoritative current structure, consult the solution/project files and the
documentation listed under **Current sources of truth** above.
