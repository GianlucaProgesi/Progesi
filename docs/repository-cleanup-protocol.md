# Progesi Toolkit — Repository Cleanup Protocol

Safe, staged process for any future repository cleanup (source or GitHub). Nothing here authorises cleanup now — it defines how cleanup must happen **when** it is approved.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — not present in the current repository and not implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`**.
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
- tests to run (baseline 64/64 must be preserved unless a change is explicitly approved),
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
