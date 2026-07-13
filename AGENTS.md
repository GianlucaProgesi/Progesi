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
| `progesi-workstream-orchestrator` | Coordination/orchestration: route work to specialist agents, prepare controlled prompts, sequence tasks | Level 1 |
| `implementation-agent-disabled` | Placeholder — **disabled** | Disabled |

## progesi-workstream-orchestrator
- **Coordination/orchestration agent**, not an implementation agent. It must not edit source code, tests, solution files, or project files.
- It **cannot autonomously work through the Task Board** — no autonomous Task Board execution and no marking implementation tasks Done.
- It **prepares controlled prompts and routes work to specialist agents** (`progesi-architecture-reviewer`, `notion-project-curator`, `cursor-task-brief-writer`, `git-governance-agent`, `regression-guard`, `grasshopper-manual-test-designer`, `documentation-writer`, and `implementation-agent-disabled` for blocked-implementation reminders only).
- It **must stop for human input gates** — ADR acceptance, source-code change requests, Cursor implementation (requires a filled task brief and human approval), GitHub deletion, AxisVar involvement, and ProgesiVariableCluster recovery all halt the agent pending human decision.
- Full definition: `.claude/agents/progesi-workstream-orchestrator.md`.

## Hard limits
- No agent may modify source code, tests, solution files, or project files without an explicitly approved task.
- No agent may mark implementation tasks Done.
- No agent may move frozen tasks out of `Blocked / Frozen`.
- No agent may perform GitHub cleanup, code cleanup, or AxisVar work.
- No agent may run Rhino or Grasshopper.

## Implementation prompt guard
If this prompt is running in Claude Code / 00. Controlled Writes, stop immediately. Implementation may run only in 05. Cursor Bridge after Cursor Allowed = true.

- 00. Controlled Writes must never execute implementation prompts.
- 05. Cursor Bridge must be a plain terminal by default.
- Cursor Agent implementation requires Cursor Allowed = true, an approved task brief, a branch, allowed files, forbidden files, tests, a rollback plan, and human approval.

## Agent completion rule
Every specialist agent must end with exactly one of:
1. No Notion update required — explain why.
2. Notion Curator packet prepared — route to 00. Controlled Writes.
3. Human input required — specify the exact Human Input row/question.
4. Unsafe to proceed — explain the stop condition.

An agent must not silently finish after changing repository, GitHub, build/test/deploy, or validation state.

## Notion Curator notification packet
At the end of a task that changes state, produce a compact packet containing:
- Task name
- Agent / tab used
- Date
- What changed
- Repository branch / commit, if applicable
- Files changed, if applicable
- PR number, if applicable
- Build/test result, if applicable
- Manual validation result, if applicable
- Human decision needed, if any
- Notion pages that may need update
- Task Board rows that may need update
- Evidence pages to create/update
- Rows that should remain open
- Rows that may be Done
- Follow-up tasks
- Stop / escalation notes

## Orchestrator routing rule
The Orchestrator must not simply ask the human to copy/paste long reports. It should:
- identify the next responsible agent
- prepare the smallest controlled prompt
- include the current Notion links / task IDs
- include the expected Notion Curator packet
- stop only when human approval, manual observation, or an architectural decision is required

## Prompt and run archival policy (summary)
Classify each controlled run output as one of: ephemeral prompt, controlled run report, reusable prompt/template, evidence record, test run record, human decision, ADR material, GitHub/PR record, manual validation evidence, strategic planning note, or archive candidate. Temporary prompts must not remain active source-of-truth content. Full policy: Notion page "Prompt and Run Archival Policy — Notion Curator Notification Loop".
