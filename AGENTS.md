# Progesi Toolkit — Agent Registry

This file documents the **planned** Claude agent model for the Progesi Toolkit.
Agents are assistants, **not autonomous project owners**. Nothing here enables autonomous execution.

## Standing constraints (apply to all agents)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — not present in the current repository and not implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`**.
- **No code cleanup is authorised yet.**

## Principles
- Agents are **planned, not autonomous project owners**.
- `implementation-agent-disabled` **remains disabled**.
- Agents must **not** work through the full Task Board autonomously.
- No implementation without explicit human approval.
- No autonomous agent execution. Each agent runs only when Gianluca explicitly asks for it by name.

## Agent maturity levels
- **Level 0 — No agents.** Current starting point; Claude operates via manually pasted prompts and controlled Notion writes.
- **Level 1 — Read-only agents.** May read Notion and/or repository context and produce reports. No writes.
- **Level 2 — Notion-only agents.** May update explicitly approved Notion pages or rows. No repository file changes.
- **Level 3 — Documentation/rules agents.** May create repository documentation or rules files only on an approved docs/rules branch. No source code changes.
- **Level 4 — Limited implementation agents.** *Future state only.* May edit code for one approved task, on one approved branch, with allowed files, forbidden files, tests, rollback plan, and human review.
- **Level 5 — Autonomous Task Board execution.** **Not approved. Not planned** for the current phase.

## Agent list
Detailed definitions live in `.claude/agents/*.md`.

| Agent | Purpose | Max level now |
|---|---|---|
| `notion-project-curator` | Maintain approved Notion pages/rows | Level 2 |
| `documentation-writer` | Repository docs/rules on approved branches | Level 3 |
| `progesi-architecture-reviewer` | Architecture & ADR-implication review | Level 1 |
| `grasshopper-manual-test-designer` | Manual Rhino/GH test procedures | Level 1 |
| `regression-guard` | Build/test reporting when instructed | Level 1 |
| `cursor-task-brief-writer` | Prepare Cursor prompts/task briefs | Level 1 |
| `git-governance-agent` | Read-only Git state / branch strategy | Level 1 |
| `progesi-strategy-planner` | Planning & task sequencing | Level 1 |
| `implementation-agent-disabled` | Placeholder — **disabled** | Disabled |

## Hard limits
- No agent may modify source code, tests, solution files, or project files without an explicitly approved task.
- No agent may mark implementation tasks Done.
- No agent may move frozen tasks out of `Blocked / Frozen`.
- No agent may perform GitHub cleanup, code cleanup, or AxisVar work.
- No agent may run Rhino or Grasshopper.
