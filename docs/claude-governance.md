# Progesi Toolkit — Claude Governance

Detailed governance for Claude Code on the Progesi Toolkit, derived from the Notion **Claude Rules Review** and **ADR-015 — Claude / Notion / Cursor / Warp governance protocol**. This document is authoritative alongside `CLAUDE.md`.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — not present in the current repository and not implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`**.
- **No code cleanup is authorised yet.**

## Tool roles
- **Claude Code (in Warp):** controlled project operation, Notion updates, documentation, audits, and — only when explicitly approved — later implementation.
- **Notion:** persistent project memory (roadmap, architecture decisions, task tracking).
- **GitHub:** version control, checkpoints, branches, release history.
- **Cursor:** read-only repository navigation / audit unless explicitly approved for implementation.
- **ChatGPT:** external architecture reviewer/planning assistant; **not** persistent project memory.

## Notion read-before-action protocol
Before any task, read the relevant Notion context and **summarize before acting**:
- Minimum set: Progesi Toolkit HQ, Claude Setup Log, current Task Board row (if any), Notion Cleanup Plan, No-Code Recovery and Validation Plan, 07_Notion_Read_Write_Protocol, 10_Human_Review_Gates.
- Architecture tasks add: Architecture Map, Progesi ADRs, ADR Coverage Review, 04_Current_Solution_Facts.
- AxisVar tasks add: 05_AxisVar_Freeze_and_Abeyance and the relevant AxisVar ADRs.
- Grasshopper tasks add: Grasshopper Component Catalogue and Grasshopper Manual Test Matrix.

Summary must cover: objective, relevant context, branch/commit, allowed scope, forbidden scope, architecture risks, tests/manual validation required, expected output, and whether human approval is required.
**If documentation conflicts, stop and report.**

## Notion write protocol
- Write to Notion **only when explicitly instructed**.
- Modify only the named page/database/rows; never touch unrelated pages or databases.
- Do not modify database schema unless explicitly instructed.
- Do not rename, move, archive, or delete pages unless explicitly instructed.
- Do not mark tasks Done without human approval.
- Prefer appending dated notes over overwriting; when replacing content, show the proposed replacement first.
- Report exactly what changed.

## Repository interaction protocol
- Inspect the repository only when instructed.
- Before interaction, confirm: current path, branch, commit, and `git status --short`.
- During handover the normally allowed repository commands are: `git status --short`, `git branch --show-current`, `git rev-parse --short HEAD`.
- Build/test commands run only when explicitly instructed.
- Repository **write** actions require an approved branch, scope, and rollback plan. Documentation/rules writes are allowed only on an approved docs/rules branch (e.g. `docs/claude-handover-rules`).
- No commits, tags, or pushes unless explicitly instructed.

## Human approval gates
Human approval is required for: code changes, source refactors, test changes, repository file creation, ADR acceptance, closure of implementation tasks, moving frozen tasks out of `Blocked / Frozen`, deleting/moving legacy code, enabling agents, running implementation loops, and Cursor implementation.

## AxisVar freeze
All ProgesiAxisVariable work is frozen and in abeyance. Frozen areas include Core model/DTO, Core persistence (`ProgesiAxisVariableRepository.cs`, `ProgesiAxisVariableSql.cs`), DataExchange axis DTOs, and the Grasshopper AxisVar components and infrastructure (AxisContext, AxisVarMapping, RhinoAxisStationing). Read-only inspection and documentation only. The freeze lifts only after relevant ADRs are accepted and Gianluca explicitly authorises source changes.

## Task Board rules
- Respect Workflow Stages. `Blocked / Frozen` tasks must not be acted on; AxisVar tasks remain `Blocked / Frozen`.
- Tasks with `Claude Allowed` unchecked must not be worked on by Claude; `Cursor Allowed` unchecked likewise for Cursor.
- `Needs Human Review` means Claude must not close the task.
- Implementation tasks must not be marked Done without human approval and test evidence.

## Manual Grasshopper validation policy
Automated .NET tests are necessary but **not sufficient** for Grasshopper confidence. For GH-facing work, consult the **Grasshopper Manual Test Matrix**. Do not claim a GH component works unless a relevant manual test exists, expected behaviour is defined, and an actual human result is recorded. AxisVar manual tests remain Frozen / Excluded.

## Loop policy
Loops must not be used for implementation during handover. The only allowed handover loop is: read context → report understanding → ask for approval → perform one approved Notion write → run `git status --short` if repository interaction occurred → stop. Future implementation loops may be considered only after rules, agents, branch policy, and tests are approved.

## Cursor handoff policy
Cursor smoke test passed; future Cursor work must use an approved task brief and remain read-only by default (no implementation without an approved brief and human approval = Approved).

## Agent policy
No autonomous Claude agents are enabled. Agents are defined in `.claude/agents/*.md` as planned capabilities only. Implementation agents remain disabled. Agents must not work through the full Task Board autonomously. See `AGENTS.md` for the maturity-level model.

## Escalation / stop rules
Stop and report when: the allowed action is unclear, Notion documentation conflicts, an unexpected file would change, scope would broaden, or an action would touch frozen/quarantined areas. When in doubt, do less and ask.
