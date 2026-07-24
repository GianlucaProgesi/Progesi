# Progesi Toolkit — Agent Registry

This file documents the **planned** Claude agent model for the Progesi Toolkit.
Agents are assistants, **not autonomous project owners**. Nothing here enables autonomous execution.

## Standing constraints (apply to all agents)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster: **Phase 1 recovered and closed** for the submitted/manual-validation scenarios; **Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered** and remain blocked. Not full release validation. See the Phase 1 exception below and the dated reconciliation section at the end of this file.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current operating baseline: **main @ `d09130a` — Functional GH Beta v0 complete, 230/230 tests passing, deployment succeeded**. Historical baseline: **88/88 at `6286aec`** (post-Cluster Phase 1 checkpoint). Historical protected source-code checkpoint: **64/64 at `376d81e`**.
- **No source-code cleanup is authorised yet** (read-only audits allowed; destructive cleanup remains gated — see the dated Post-Beta v0 reconciliation at the end of this file).

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

## ProgesiVariableCluster Phase 1 exception

The general ProgesiVariableCluster recovery freeze remains in force except for the approved Phase 1 file-scoped recovery on branch feat/cluster-recovery-portscope.

Agents may route Phase 1 only if:
- Cursor Allowed = true
- the Phase 1 Task Board row is Ready for Cursor
- the persisted Cursor Task Brief v1.0 is used
- execution is in 05. Cursor Bridge
- AxisVar files remain forbidden
- Phase 2/3 remain blocked

The Orchestrator may prepare/reroute Phase 1 prompts but must not execute implementation.

## Current operating baseline and status — reconciliation (2026-07-15)

This section reconciles the standing constraints above with the current post-Cluster-Phase-1 / post-ADR-acceptance state. Where earlier text names `376d81e` / 64/64 as the *current* baseline, treat that wording as **historical**. This section does **not** weaken any standing constraint, the AxisVar freeze, the implementation prompt guard, the Phase 1 exception, or any agent maturity/limit. It grants no new authorisation and enables no autonomy.

1. **Baselines.** *(Superseded 2026-07-23 — the current baseline is now `main` @ `d09130a`, 230/230; see the Post-Beta v0 reconciliation below.)* As recorded on 2026-07-15, the operating baseline was **88/88 tests passing at `6286aec`** (after PR #63 / Cluster Phase 1), with the historical protected source-code checkpoint **64/64 at `376d81e`** on `feat/axis-variable-core`.

2. **ProgesiVariableCluster.** Phase 1 is **recovered and closed** for the submitted/manual-validation scenarios (GH-CLUSTER-001..004 recorded Passed). **Phase 2 (SQLite) and Phase 3 (EF / DataExchange) are not recovered and remain blocked** until separately approved. Not a full release-validation sign-off. The Phase 1 exception above remains in force; the Orchestrator still must not execute implementation.

3. **ADR acceptance posture.** The three consolidation ADRs are **Accepted as interim / direction-setting** only (no implementation authority): DataExchange (interim A+E interchange boundary; target Option D); EF/SQLite (EF long-term target; SQLite interim canonical; EF retirement deferred); ProgesiDomainServices / ADR-009 (Option C direction accepted, consolidation planned not implemented). Agents may reference these directions but must still route any implementation through an approved brief, branch, tests, and human approval in 05. Cursor Bridge.

4. **AxisVar.** Remains **frozen and in abeyance**. No agent may touch AxisVar.

5. **Agents and Notion Curator.** All agents remain **controlled and human-gated** at their documented maturity levels; no autonomous Task Board execution; `implementation-agent-disabled` stays disabled; the Notion Curator operates only within its approved controlled-write scope.

6. **`main` and any beta/release line.** *(Superseded 2026-07-23.)* At the 2026-07-15 reconciliation this was future-only with no merge to `main`. **This no longer holds:** Functional GH Beta v0 is now integrated into `main` at `d09130a` (via PR #72). See the Post-Beta v0 reconciliation section below.

7. **No new authorisation.** This section records state only; it authorises no implementation, cleanup, ADR-driven code change, branch/tag cleanup, or scope broadening.

## Post-Beta v0 reconciliation — 2026-07-23

This section records the current operating state after **Functional GH Beta v0** was integrated into `main`. It supersedes earlier wording that treats `main` as untouched/future-only or the mode as "no-code handover". It does **not** weaken the AxisVar freeze, the implementation prompt guard, the Phase 1 exception, any agent maturity level, or any hard limit. **It grants no new authorisation and enables no autonomy.**

1. **Baselines (retiered).** Current operating baseline: `main` @ `d09130a` — Functional GH Beta v0 complete, **230/230 tests passing**, deployment succeeded. Historical baseline: **88/88 at `6286aec`** (post-Cluster Phase 1). Historical protected source-code checkpoint: **64/64 at `376d81e`**.

2. **Current-State source of truth.** The Notion page **"Progesi Current State — Post Functional GH Beta v0"** is canonical. Agents and future Claude sessions should start from that page, the active Task Board rows, the Architecture Map, the Roadmap, and `git status`.

3. **Posture.** The earlier no-code handover posture is **superseded**; the current posture is post-beta consolidation / Claude handover / cleanup governance. Agents remain **controlled and human-gated** at their documented maturity levels; no autonomous Task Board execution; `implementation-agent-disabled` stays disabled.

4. **`main` status.** `main` is no longer untouched — Functional GH Beta v0 is integrated at `d09130a` (PR #72). Item 6 of the 2026-07-15 reconciliation ("future-only") is historical and superseded here.

5. **GitHub / Notion cleanup.** Audit-first. Safe first passes and read-only audits have occurred (GitHub Cleanup Audit 366, settings verification 369A). Destructive cleanup remains gated: no branch/tag deletion, no Notion archive/delete/move, no ADR status change, no schema change, no AxisVar work without explicit approval. No agent may perform GitHub or code cleanup.

6. **Preserved.** AxisVar freeze, historical checkpoints, ADR references and posture, all human-approval gates, agent maturity limits, and protected/staged workflow language remain in force.

7. **No new authorisation.** State record only; no implementation, cleanup, ADR-driven code change, Notion deletion, or scope broadening is authorised, and no agent autonomy is enabled.

## Autonomous Operating Charter reconciliation — 2026-07-24

This section reconciles the agent registry with the **Autonomous Operating Charter & Standing Green Authorisation** (Notion, 08 — Governance and Tooling). It **does not** raise any agent's maturity level, enable autonomous Task Board execution, re-enable `implementation-agent-disabled`, or weaken any hard limit, the AxisVar freeze, the implementation prompt guard, or the Phase 1 exception. **It grants no new authorisation and enables no agent autonomy.**

1. **Where the tiers apply.** The Charter's **Green / Amber / Red** tiers govern the *ceremony* of controlled work, not agent maturity. Standing **Green** (routine Notion hygiene, Current-State / Strategic-Planning-Log / change-log maintenance, `{}` resolution) maps to work already inside the `notion-project-curator`'s Level-2 controlled-write scope — it removes the per-step prompt, not the scope boundary. **Amber** still requires one explicit human go per package. **Red** (branch/tag deletion, ADR status, schema, source code, Cursor implementation, AxisVar) still requires recorded Human Input and remains outside every agent's authority.

2. **Agent maturity unchanged.** All agents remain at their documented maximum levels (see the Agent list). `implementation-agent-disabled` stays disabled. No agent may work through the Task Board autonomously or mark implementation tasks Done. The standing Green authorisation does **not** promote any agent.

3. **`{@Claude …}` marker.** Agents that read Notion pages must honour the refined `{}` protocol: `{@Claude <question>}` is answered inline; `{@Claude <action>}` is executed only if Green, otherwise converted to `{⛔}` and escalated. Agents never delete or rewrite original human text.

4. **Change-log obligation.** Any agent whose task changes state must ensure its Notion Curator packet feeds the dual change log (Notion *ChatGPT Sync — Change Log* + on-disk mirror), consistent with the Agent completion rule above.

5. **Preserved.** All hard limits, maturity levels, the AxisVar freeze, and the human-gated posture remain in force exactly as written.

6. **No new authorisation.** Governance-model record only.
