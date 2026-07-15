# Progesi Toolkit — Claude ↔ Cursor Interface Protocol

This document defines the boundary between Claude and Cursor. It complements `docs/claude-governance.md` and `.cursor/rules/progesi-architecture.mdc`.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster: **Phase 1 recovered and closed** for the submitted/manual-validation scenarios; **Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered** and remain blocked. Not full release validation. See the Phase 1 recovery exception in `CLAUDE.md`.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current operating baseline: **88/88 passing at `6286aec`** (after PR #63 / Cluster Phase 1); the historical protected source-code checkpoint remains **64/64 at `376d81e`**.
- **No code cleanup is authorised yet.**

## No direct control requirement
There is **no requirement for Claude to control Cursor directly**. Claude does not drive Cursor. The two tools communicate through Notion and human review, not through automation.

## The interface
The approved flow is one direction, gated by human review:

```
Notion (decision & context)
  → Claude prepares a task brief (when instructed)
  → Human approves the brief
  → Cursor implements (only within the approved scope)
  → Claude reviews Cursor output (diff + tests)
  → Human reviews
  → Claude updates Notion (only when explicitly instructed)
```

## Cursor is read-only by default
- Cursor remains **read-only** unless implementation is explicitly approved.
- Cursor must obey the rules in `.cursor/rules/progesi-architecture.mdc`.
- Cursor must **not invent architecture** — it implements only what an approved brief specifies.

## Implementation prerequisites
Cursor may implement only when **all** of the following exist:
- an **approved Task Board row**
- **human approval status = Approved** (explicit, not merely implied by the workflow)
- a **branch**
- **allowed files**
- **forbidden files**
- **tests** to run
- **manual validation** if Grasshopper is affected
- a **rollback plan**

If any prerequisite is missing, implementation does not proceed.

## Cursor smoke-test hardening
- Cursor verified the governance files are readable.
- Minor hardening was added after the smoke test.
- Cursor remains read-only unless all prerequisites above are met (including human approval status = Approved).

## Claude's review responsibility
After Cursor implements, Claude reviews the diff and test results against the brief and the architecture rules before any merge or Notion update. Anything outside the approved scope is rejected and reported.

## Implementation prompt guard
If this prompt is running in Claude Code / 00. Controlled Writes, stop immediately. Implementation may run only in 05. Cursor Bridge after Cursor Allowed = true.

- 00. Controlled Writes must never execute implementation prompts.
- 05. Cursor Bridge must be a plain terminal by default.
- Cursor Agent implementation requires Cursor Allowed = true, an approved task brief, a branch, allowed files, forbidden files, tests, a rollback plan, and human approval.

## Notion Curator reporting (Cursor)
- Cursor must include a Notion Curator packet in its final report.
- Cursor must not update Notion directly unless separately authorised.
- Cursor must not mark tasks Done.
- Cursor implementation reports must be routed back to 00. Controlled Writes for Notion recording.
