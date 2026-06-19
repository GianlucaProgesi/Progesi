# Progesi Toolkit — Claude ↔ Cursor Interface Protocol

This document defines the boundary between Claude and Cursor. It complements `docs/claude-governance.md` and `.cursor/rules/progesi-architecture.mdc`.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — not present in the current repository and not implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`**.
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
- a **branch**
- **allowed files**
- **forbidden files**
- **tests** to run
- **manual validation** if Grasshopper is affected
- a **rollback plan**

If any prerequisite is missing, implementation does not proceed.

## Claude's review responsibility
After Cursor implements, Claude reviews the diff and test results against the brief and the architecture rules before any merge or Notion update. Anything outside the approved scope is rejected and reported.
