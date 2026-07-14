# Progesi Toolkit — Claude Operating Rules

These rules govern how Claude Code may work in this repository. They are authoritative.
If anything here conflicts with Notion, **stop and report** before acting.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — it is not present in the current repository and must not be treated as implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`** (protected checkpoint on `feat/axis-variable-core`).
- **No code cleanup is authorised yet.**

## 1. Current mode
- Mode: **no-code handover / governance setup**.
- Protected checkpoint: branch **`feat/axis-variable-core`** at commit **`376d81e`** (clean tree, release build passing, 64/64 tests).
- The current branch **`docs/claude-handover-rules`** is for **documentation/rules only**. No source code, tests, solution files, or project files may be changed on it.

## 2. Non-negotiable rules
- No source code changes unless explicitly approved.
- No tests modified unless explicitly approved.
- No AxisVar work of any kind.
- No legacy removal (code or files).
- No GitHub cleanup (no branch/tag deletion, no history rewrite, no force-push).
- No autonomous Task Board execution.
- Do not mark implementation tasks **Done** without human approval and test evidence.
- Do not broaden task scope or perform opportunistic refactors.
- Do not rename public classes, namespaces, files, or projects without approval.

## 3. Required Notion read-before-action protocol
Before acting, read the relevant Notion context:
- **Progesi Toolkit HQ**
- **Claude Setup Log**
- the **current Task Board row**, if a task is involved
- **Architecture Map**, if architecture is involved
- **05_AxisVar_Freeze_and_Abeyance**, if AxisVar is involved
- **10_Human_Review_Gates**

Then **summarize before acting**: objective, relevant context, branch/commit, allowed scope, forbidden scope, architecture risks, tests/manual validation required, expected output, and whether human approval is required.
If Notion documentation conflicts, **stop and report** — do not proceed.

## 4. Architecture rules
- ProgesiCore must **not** depend on Rhino, Grasshopper, Excel libraries, Entity Framework, UI frameworks, SQLite-specific NuGet packages, or ASP.NET.
- **DataExchange is not Core** — it is the interchange boundary and must stay outside the Core domain.
- Persistence implementation should **not** be added to Core.
- Existing AxisVar persistence inside Core (`src/ProgesiCore/Persistence/ProgesiAxisVariable*.cs`) is **quarantined** — do not extend it.
- Dependency direction: Grasshopper/Rhino/Excel/Database/Future-Web → Application/Adapter → DataExchange/Repository interfaces → Core. Never the reverse.

## 5. AxisVar freeze
All ProgesiAxisVariable work is frozen. The frozen areas (do not modify, delete, consolidate, move, or wire) include:
- `src/ProgesiCore/ProgesiAxisVariable.cs`
- `src/ProgesiCore/ProgesiAxisVariableDto.cs`
- `src/ProgesiCore/Persistence/ProgesiAxisVariableRepository.cs`
- `src/ProgesiCore/Persistence/ProgesiAxisVariableSql.cs`
- axis-related DTOs in `src/ProgesiDataExchange/`
- `src/ProgesiGrasshopperAssembly/Components/AxisVarDefineComponent.cs`
- `src/ProgesiGrasshopperAssembly/Components/AxisVarSeriesComponent.cs`
- `src/ProgesiGrasshopperAssembly/Infrastructure/AxisVar/*` (AxisContext, AxisVarMapping, RhinoAxisStationing)
- axis-related tests

Read-only inspection and documentation are allowed. The freeze lifts only after the relevant ADRs are accepted and Gianluca explicitly authorises source changes.

## 6. Cursor boundary
- Cursor is **read-only** unless explicitly approved.
- Claude prepares Cursor task briefs **only when instructed**.
- Cursor implementation requires: an approved Task Board row, a branch, allowed files, forbidden files, tests, manual validation if Grasshopper is affected, and a rollback plan.
- Claude reviews Cursor output (diff + test results) before any merge or documentation update.
- Cursor smoke test passed, but Cursor still requires an explicit task brief and human approval before any implementation.

## 7. Build/test commands
- Run build/test **only when explicitly instructed**.
- Canonical baseline: `dotnet build -c Release` then `dotnet test` → **64 tests passing** at commit `376d81e`.
- Never run Rhino or Grasshopper from here.

## 8. Reporting requirements
After any action, report:
- changed files
- Notion updates made (if any)
- commands run
- test results (if any)
- `git status` (and branch/commit when repository interaction occurred)

## 9. Implementation prompt guard
If this prompt is running in Claude Code / 00. Controlled Writes, stop immediately. Implementation may run only in 05. Cursor Bridge after Cursor Allowed = true.

- 00. Controlled Writes must never execute implementation prompts.
- 05. Cursor Bridge must be a plain terminal by default.
- Cursor Agent implementation requires Cursor Allowed = true, an approved task brief, a branch, allowed files, forbidden files, tests, a rollback plan, and human approval.

## ProgesiVariableCluster Phase 1 recovery exception

ProgesiVariableCluster remains a missing capability / suspected regression and must not be treated as generally implemented.

Exception:
A human-approved, file-scoped Phase 1 recovery is authorised only on branch feat/cluster-recovery-portscope, only under the persisted "Cursor Task Brief v1.0 — ProgesiVariableCluster Recovery Phase 1 (file-scoped port)", and only after Cursor Allowed = true and the approved Task Board row is Ready for Cursor.

This exception does not authorise:
- AxisVar work
- wholesale branch merge
- Phase 2 SQLite recovery
- Phase 3 EF/DataExchange recovery
- source cleanup
- ADR acceptance
- branch/tag cleanup
- treating Cluster as fully implemented before build/test/manual validation and human review

AxisVar remains frozen.

Phase 2 and Phase 3 remain blocked until separately approved.
