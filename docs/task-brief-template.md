# Progesi Toolkit — Task Brief Template

Reusable template for preparing a controlled implementation or documentation task.
Copy this template per task. A brief is only actionable after **Human approval status = Approved**.

## Standing constraints (apply to every brief)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster is a **missing capability / suspected regression** — not present in the current repository and not implemented.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current test baseline: **64/64 passing at commit `376d81e`**.
- **No code cleanup is authorised yet.**

---

## Task title
<short, specific title>

## Notion task link
<URL of the Progesi Task Board row>

## Branch
<exact branch name; must already be approved>

## Objective
<one paragraph: what outcome this task achieves and why>

## Layer
<one or more of: Core, DataExchange, Grasshopper-Rhino, Database, Excel, Tests, Docs, GitHub, Cursor, Future Web>

## Allowed files
<explicit list of files that may be created or modified>

## Forbidden files
<explicit list; always includes AxisVar files, ProgesiCore/Persistence, ProgesiDomainServices duplication, solution/project files unless explicitly listed>

## Architecture constraints
- Preserve Core independence (no Rhino/GH/Excel/EF/UI/SQLite-specific/ASP.NET dependencies in Core).
- DataExchange stays out of Core.
- No persistence implementation added to Core.
- No public API renames without approval.

## Acceptance criteria
<bullet list of objectively checkable outcomes>

## Tests to run
<exact commands, e.g. `dotnet build -c Release`, `dotnet test`; expected: baseline 64/64 unless the task adds tests>

## Manual validation required
<Grasshopper Manual Test Matrix rows / procedures, if GH is affected; otherwise "None">

## Regression risks
<what could break; which existing behaviour to watch>

## Rollback plan
<how to revert safely if the change fails review or tests>

## Human approval status
- Not requested
- Requested
- Approved
- Rejected

Record the selected value with date and approver. Implementation may proceed only when this is **Approved**.

## Current branch policy
- Working branch: <name>
- Is this branch allowed for implementation? <yes / no>
- If not (e.g. a documentation/rules branch such as `docs/claude-handover-rules`), **stop** — do not implement here.

## Claude review checklist
- [ ] Only allowed files changed
- [ ] No forbidden/frozen files touched
- [ ] Architecture constraints respected
- [ ] Tests run and results recorded (baseline preserved or intentionally updated)
- [ ] Manual GH validation recorded if applicable
- [ ] `git status` / diff reviewed
- [ ] Notion updated only as instructed

## Cursor instructions
<precise, scoped prompt for Cursor; must reference allowed/forbidden files and forbid architecture invention and out-of-scope edits>

## Final reporting checklist
After the task, report:
- [ ] Files changed
- [ ] Commands run
- [ ] Tests run and results
- [ ] Final `git status`
- [ ] Deviations from the allowed scope (if any)
- [ ] Any blocked or unclear instruction

## Controlled-run governance
- [ ] Implementation prompt guard acknowledged: "If this prompt is running in Claude Code / 00. Controlled Writes, stop immediately. Implementation may run only in 05. Cursor Bridge after Cursor Allowed = true."
- [ ] Notion Curator packet required after completion (route to 00. Controlled Writes)
- Prompt/run artefact classification: <ephemeral prompt | controlled run report | reusable prompt/template | evidence record | test run record | human decision | ADR material | GitHub/PR record | manual validation evidence | strategic planning note | archive candidate>
- Tab where this task must run: <00. Controlled Writes | 05. Cursor Bridge | 07. Build Test Deploy | 08. Grasshopper Validation | planning tab>
- May this task be run by: <Claude | Cursor | neither> (Cursor only with Cursor Allowed = true and an approved brief)
