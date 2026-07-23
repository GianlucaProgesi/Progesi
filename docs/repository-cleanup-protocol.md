# Progesi Toolkit — Repository Cleanup Protocol

Safe, staged process for any future repository cleanup (source or GitHub). Nothing here authorises destructive cleanup now — it defines how cleanup must happen **when** it is approved. Post-beta, read-only **audit-first passes have already occurred** (e.g. GitHub Cleanup Audit 366 and branch-protection settings verification 369A); destructive cleanup (branch/tag deletion, Notion archive/delete/move, ADR/schema changes, AxisVar work) remains **gated** pending explicit approval.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster: **Phase 1 recovered and closed** for the submitted/manual-validation scenarios; **Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered** and remain blocked. Not full release validation. See the Phase 1 recovery exception in `CLAUDE.md`.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current operating baseline: **main @ `d09130a` — Functional GH Beta v0 complete, 230/230 tests passing, deployment succeeded**. Historical baseline: **88/88 at `6286aec`** (post-Cluster Phase 1); historical protected source-code checkpoint: **64/64 at `376d81e`**.
- **No code cleanup is authorised yet.**

## Core principles
- **Cleanup starts with a read-only audit.** No changes until the audit is reviewed.
- **Classify cleanup candidates** before touching anything: what it is, why it looks removable, what depends on it, and the risk of removing it.
- **No deletion without ADR + validation + rollback.** Code/files are not removed merely because they look wrong.
- **GitHub cleanup is separate from source-code cleanup.** They are different tasks with different risks and must not be combined.
- **Do not rewrite history.** No rebases that drop commits, no force-pushes.
- **Do not delete tags or releases yet.**

## Source cleanup requirements
Any source cleanup requires:
- an approved branch and explicit scope (allowed/forbidden files),
- tests to run (current operating baseline 230/230 on `main` @ `d09130a` must be preserved unless a change is explicitly approved),
- manual Grasshopper validation if any GH-facing code is affected,
- a rollback plan,
- human approval.

### Quarantined — do not delete yet
- `src/ProgesiCore/Persistence/ProgesiAxisVariableRepository.cs`
- `src/ProgesiCore/Persistence/ProgesiAxisVariableSql.cs`
- all AxisVar files (Core, DataExchange DTOs, Grasshopper components and infrastructure)
- ProgesiDomainServices duplicate-model code (review first)
- duplicated DataExchange paths (review first)

These hold architectural clues; removing them now is a destructive source change and is forbidden.

## GitHub branch cleanup (read-only audit first)
Inventory branches and tags, then classify each branch as exactly one of:
- **Keep**
- **Review**
- **Delete local later**
- **Delete remote later**
- **Unknown**

Rules: no deletion, no force-push, no history rewrite, no tag removal during the audit. Local stale-branch deletion happens only after approval; remote stale-branch deletion happens only after separate approval. Source code is never removed as part of GitHub cleanup.

## Order of operations (when approved)
1. Read-only audit + classification.
2. Human review of the audit.
3. Protect essential branches/tags.
4. Approve specific, scoped removals.
5. Execute removals on an approved branch with rollback ready.
6. Re-run tests / record results.
