# Progesi Toolkit — Claude Operating Rules

These rules govern how Claude Code may work in this repository. They are authoritative.
If anything here conflicts with Notion, **stop and report** before acting.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster: **Phase 1 recovered and closed** for the submitted/manual-validation scenarios (Core model/service, InMemory repository, narrow Rhino support repository, ClusterDef/ClusterOut components, `ProgesiClusters` Excel export, and Cluster tests). **Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered** and remain blocked. This is not full release validation. See the Phase 1 recovery exception below and the dated reconciliation section at the end of this file.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current operating baseline: **main @ `d09130a` — Functional GH Beta v0 complete, 230/230 tests passing, deployment succeeded**. Historical baseline: **88/88 at `6286aec`** (post-Cluster Phase 1 checkpoint). Historical protected source-code checkpoint: **64/64 at `376d81e`** on `feat/axis-variable-core`.
- **No source-code cleanup is authorised yet** (read-only audits are allowed; destructive cleanup remains gated — see the dated Post-Beta v0 reconciliation at the end of this file).

## 1. Current mode
- Mode: **post-beta consolidation / Claude handover / cleanup governance**. The earlier **no-code handover / governance setup** posture is **superseded** — see the dated Post-Beta v0 reconciliation at the end of this file.
- Historical protected checkpoint: branch **`feat/axis-variable-core`** at commit **`376d81e`** (clean tree, release build passing, 64/64 tests) — retained as a historical reference, not the current baseline.
- Documentation/rules changes continue to run on approved docs/rules branches (e.g. the current `docs/post-beta-governance-reconciliation`). No source code, tests, solution files, or project files may be changed on such a branch.

## 2. Non-negotiable rules
- No source code changes unless explicitly approved.
- No tests modified unless explicitly approved.
- No AxisVar work of any kind.
- No legacy removal (code or files).
- No destructive GitHub cleanup (no branch/tag deletion, no history rewrite, no force-push) without explicit approval. Read-only audits are allowed, and audit-first passes have already occurred (see the dated Post-Beta v0 reconciliation below).
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
- Canonical commands: `dotnet build -c Release` then `dotnet test`. Current operating baseline: **230/230 passing on `main` @ `d09130a`** (Functional GH Beta v0). Historical baseline: **88/88 at `6286aec`** (post-Cluster Phase 1). Historical protected source-code checkpoint: **64/64 at `376d81e`**.
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

## Current operating baseline and status — reconciliation (2026-07-15)

This section reconciles the standing constraints and mode notes above with the current post-Cluster-Phase-1 / post-ADR-acceptance state. Where earlier text names `376d81e` / 64/64 as the *current* baseline, or describes the mode as "no-code handover", treat that wording as **historical**; the current operating state is recorded here. This section does **not** weaken any standing constraint, the AxisVar freeze, the implementation prompt guard (§9), or the ProgesiVariableCluster Phase 1 recovery exception above. It grants no new authorisation.

1. **Baselines.** *(Superseded 2026-07-23 — the current baseline is now `main` @ `d09130a`, 230/230; see the Post-Beta v0 reconciliation below.)* As recorded on 2026-07-15, the operating baseline was **88/88 tests passing at `6286aec`** (state after the PR #63 / Cluster Phase 1 merge), with the historical protected source-code checkpoint **64/64 at `376d81e`** on `feat/axis-variable-core`. §1 "Current mode" predates ADR acceptance and Cluster Phase 1 and is superseded by this section for current-state purposes.

2. **ProgesiVariableCluster.** Phase 1 is **recovered and closed** for the submitted and manually validated scenarios (Core model/service, InMemory repository, narrow Rhino support repository, ClusterDef/ClusterOut Grasshopper components, `ProgesiClusters` Excel export, Cluster tests; GH-CLUSTER-001..004 recorded Passed). **Phase 2 (SQLite persistence) and Phase 3 (EF / DataExchange) are not recovered and remain blocked** until separately approved. This is not a full release-validation sign-off. The Phase 1 recovery exception above remains in force exactly as written.

3. **ADR acceptance posture.** The three consolidation ADRs are now **Accepted as interim / direction-setting** — acceptance sets direction only and authorises no implementation:
   - **DataExchange ADR** — interim: keep DataExchange as the interchange boundary (Options A + E); long-term target is Option D.
   - **EF / SQLite ADR** — EF is the **long-term target**; the SQLite repository remains the **interim canonical** persistence; EF retirement is deferred and there is no near-term SQLite retirement.
   - **ProgesiDomainServices ADR (ADR-009)** — Option C direction accepted; consolidation is planned, not yet implemented.
   Any implementation flowing from these ADRs still requires an approved Task Board row, a branch, a task brief, tests, and human approval, and must run in 05. Cursor Bridge — never in 00. Controlled Writes.

4. **AxisVar.** Remains **frozen and in abeyance** exactly as in §5. Nothing here lifts that freeze.

5. **Agents and Notion Curator.** All agents remain **controlled and human-gated** at their documented maturity levels (see `AGENTS.md`); no autonomous Task Board execution; implementation agents remain disabled; the Notion Curator operates only within its approved controlled-write scope.

6. **`main` and any beta/release line.** *(Superseded 2026-07-23.)* At the time of the 2026-07-15 reconciliation, `main` was untouched and this work was future-only. **This no longer holds:** Functional GH Beta v0 is now integrated into `main` at `d09130a` (via PR #72). See the Post-Beta v0 reconciliation section below.

7. **No new authorisation.** This section records state; it authorises no code cleanup, no ADR-driven implementation, no branch/tag cleanup, and no scope broadening.

## Post-Beta v0 reconciliation — 2026-07-23

This section records the current operating state after **Functional GH Beta v0** was integrated into `main`. It supersedes any earlier wording — including §1 "Current mode" and the 2026-07-15 reconciliation — that describes the mode as "no-code handover" or `main` as untouched/future-only. It does **not** weaken the AxisVar freeze, the implementation prompt guard (§9), the ProgesiVariableCluster Phase 1 recovery exception, or any authorisation gate. **It grants no new authorisation.**

1. **Baselines (retiered).**
   - **Current operating baseline:** `main` @ `d09130a` — Functional GH Beta v0 complete, **230/230 tests passing**, deployment succeeded.
   - **Historical baseline:** **88/88 at `6286aec`** — post-Cluster Phase 1 checkpoint.
   - **Historical protected source-code checkpoint:** **64/64 at `376d81e`** on `feat/axis-variable-core`.

2. **Current-State source of truth.** The Notion page **"Progesi Current State — Post Functional GH Beta v0"** is canonical. Future Claude sessions should start from that page, the active Task Board rows, the Architecture Map, the Roadmap, and `git status` — not from prior chat/session memory.

3. **Posture.** The earlier **no-code handover / governance setup** posture is **superseded**. The current posture is **post-beta consolidation / Claude handover / cleanup governance**.

4. **`main` status.** `main` is **no longer untouched**. Functional GH Beta v0 is integrated into `main` at `d09130a` (via PR #72). Any statement elsewhere in this file that `main` is untouched or future-only is historical and superseded by this section.

5. **GitHub / Notion cleanup.** Cleanup remains **audit-first**. Safe first passes and read-only audits have occurred (e.g. GitHub Cleanup Audit 366 and branch-protection settings verification 369A). **Destructive cleanup remains gated:** no branch/tag deletion, no Notion archive/delete/move, no ADR status change, no schema change, and no AxisVar work without explicit approval.

6. **Preserved.** The AxisVar freeze, the historical checkpoints (`6286aec`, `376d81e`), all ADR references and their acceptance posture, every human-approval/authorisation gate, and the protected/staged workflow language all remain in force exactly as written above.

7. **No new authorisation.** This section records state only. It authorises no code cleanup, no ADR-driven implementation, no branch/tag cleanup, no Notion deletion, and no scope broadening.
