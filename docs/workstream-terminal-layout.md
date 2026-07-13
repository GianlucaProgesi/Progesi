# Workstream Terminal Layout — Progesi Toolkit

## Purpose

Define the recommended Warp terminal layout for Claude-assisted Progesi development.

## Terminal 1 — Strategic Planning

Purpose:
Human + Claude strategic thinking.

Allowed:
planning, brainstorming, high-level decisions.

Forbidden:
repository edits, Cursor implementation, broad cleanup.

## Terminal 2 — Workstream Orchestrator

Agent:
progesi-workstream-orchestrator.

Purpose:
sequence tasks, prepare prompts, coordinate specialist agents.

Forbidden:
implementation, branch deletion, ADR acceptance.

## Terminal 3 — Architecture ADR

Agent:
progesi-architecture-reviewer.

Purpose:
ADR options, architecture reviews, dependency boundaries.

Forbidden:
source edits.

## Terminal 4 — Notion Curator

Agent:
notion-project-curator.

Purpose:
controlled Notion updates, Task Board hygiene, Human Input review.

Forbidden:
parallel writes to same page/database, broad deletion.

## Terminal 5 — Cursor Bridge

Agent:
cursor-task-brief-writer.

Purpose:
prepare task briefs and read-only Cursor Agent checks.

Allowed command shape for read-only Cursor Agent:
cursor-agent --print --mode ask --output-format text

Forbidden:
--trust, --force, --yolo, --approve-mcps unless separately approved.

## Terminal 6 — Git Governance

Agent:
git-governance-agent.

Purpose:
branch/tag audits, PR sequencing, branch strategy.

Forbidden:
branch deletion, force-push, history rewrite unless explicitly approved.

## Terminal 7 — Regression Guard

Agent:
regression-guard.

Purpose:
controlled build/test reporting.

Typical commands:
dotnet build -c Release
dotnet test

Only run when instructed.

## Terminal 8 — Grasshopper Validation

Agent:
grasshopper-manual-test-designer.

Purpose:
manual test design and reporting.

Forbidden:
claiming pass/fail without human Rhino/Grasshopper evidence.

## Parallel work rules

- Planning terminals may operate in parallel.
- Notion writes must be serialized.
- Repository writes must be serialized.
- Cursor implementation must be one approved task at a time.
- Human input gates override agent momentum.

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
At the end of a task that changes state, produce a compact packet containing: task name; agent / tab used; date; what changed; repository branch / commit (if applicable); files changed (if applicable); PR number (if applicable); build/test result (if applicable); manual validation result (if applicable); human decision needed (if any); Notion pages that may need update; Task Board rows that may need update; evidence pages to create/update; rows that should remain open; rows that may be Done; follow-up tasks; stop / escalation notes.

## Orchestrator routing rule
The Orchestrator must not simply ask the human to copy/paste long reports. It should:
- identify the next responsible agent
- prepare the smallest controlled prompt
- include the current Notion links / task IDs
- include the expected Notion Curator packet
- stop only when human approval, manual observation, or an architectural decision is required

## Prompt and run archival policy (summary)
Classify each controlled run output as one of: ephemeral prompt, controlled run report, reusable prompt/template, evidence record, test run record, human decision, ADR material, GitHub/PR record, manual validation evidence, strategic planning note, or archive candidate. Temporary prompts must not remain active source-of-truth content. Full policy: Notion page "Prompt and Run Archival Policy — Notion Curator Notification Loop".
