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
