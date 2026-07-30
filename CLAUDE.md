# Progesi Toolkit — Claude Operating Rules

These rules govern how Claude Code may work in this repository. They are authoritative.
If anything here conflicts with Notion, **stop and report** before acting.

## Standing constraints (apply to all work)
- AxisVar remains **frozen** and in abeyance — no modification, deletion, DTO consolidation, persistence move, or Grasshopper wiring.
- ProgesiVariableCluster: **Phase 1 recovered and closed** for the submitted/manual-validation scenarios (Core model/service, InMemory repository, narrow Rhino support repository, ClusterDef/ClusterOut components, `ProgesiClusters` Excel export, and Cluster tests). **Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered** and remain blocked. This is not full release validation. See the Phase 1 recovery exception below and the dated reconciliation section at the end of this file.
- DataExchange is **not** a Core domain object — it is the interchange boundary.
- Current operating baseline: **main @ `0c2abf1` — 303 tests passing (+19 opt-in stress skipped), 0 failed** (post-R2-C line: DataExchange extracted to `Progesi.LiveDataExchange`, SQLite + EF geometry parity, ClusterOut file-open fix). Historical baselines: **230/230 at `d09130a`** (Functional GH Beta v0), **88/88 at `6286aec`** (post-Cluster Phase 1). Historical protected source-code checkpoint: **64/64 at `376d81e`** on `feat/axis-variable-core` (per **ADR-010 (Superseded)**, `376d81e` is a historical checkpoint *commit*; the branch tip is `6d51987`, merged to main + backed up on origin — see PR #89). See the dated 2026-07-29 reconciliation at the end of this file.
- **No source-code cleanup is authorised yet** (read-only audits are allowed; destructive cleanup remains gated — see the dated Post-Beta v0 reconciliation at the end of this file).

## 1. Current mode
- Mode: **post-beta consolidation / Claude handover / cleanup governance**. The earlier **no-code handover / governance setup** posture is **superseded** — see the dated Post-Beta v0 reconciliation at the end of this file.
- Historical protected checkpoint: branch **`feat/axis-variable-core`** at commit **`376d81e`** (clean tree, release build passing, 64/64 tests) — retained as a historical reference, not the current baseline.
- Documentation/rules changes continue to run on approved docs/rules branches (e.g. the current `docs/post-beta-governance-reconciliation`). No source code, tests, solution files, or project files may be changed on such a branch.
- **Autonomy is tiered** per the **Autonomous Operating Charter & Standing Green Authorisation** (Notion, 08 — Governance): **Green** = pre-authorised routine Notion hygiene / Current-State + change-log maintenance / `{}` marker resolution (no per-step prompt); **Amber** = one explicit human go per scoped package; **Red** = explicit Human Input recorded before acting. See the dated Charter reconciliation at the end of this file. The Charter grants **no** new source-code, cleanup, ADR, branch/tag, or AxisVar authority.

## 2. Non-negotiable rules
- No source code changes unless explicitly approved.
- No tests modified unless explicitly approved.
- No AxisVar work of any kind.
- No legacy removal (code or files).
- No destructive GitHub cleanup (no branch/tag deletion, no history rewrite, no force-push) without explicit approval. Read-only audits are allowed, and audit-first passes have already occurred (see the dated Post-Beta v0 reconciliation below).
- No autonomous Task Board execution.
- Do not mark implementation tasks **Done** without human approval and test evidence.
- Do not broaden task scope or perform opportunistic refactors.
- Do not rename public classes, namespaces, files, or projects without approval.

## 3. Required Notion read-before-action protocol
Before acting, read the relevant Notion context:
- **Progesi Toolkit HQ**
- **Claude Setup Log**
- the **current Task Board row**, if a task is involved
- **Architecture Map**, if architecture is involved
- **05_AxisVar_Freeze_and_Abeyance**, if AxisVar is involved
- **10_Human_Review_Gates**

Then **summarize before acting**: objective, relevant context, branch/commit, allowed scope, forbidden scope, architecture risks, tests/manual validation required, expected output, and whether human approval is required.
If Notion documentation conflicts, **stop and report** — do not proceed.

## 4. Architecture rules
- ProgesiCore must **not** depend on Rhino, Grasshopper, Excel libraries, Entity Framework, UI frameworks, SQLite-specific NuGet packages, or ASP.NET.
- **DataExchange is not Core** — it is the interchange boundary and must stay outside the Core domain.
- Persistence implementation should **not** be added to Core.
- Existing AxisVar persistence inside Core (`src/ProgesiCore/Persistence/ProgesiAxisVariable*.cs`) is **quarantined** — do not extend it.
- Dependency direction: Grasshopper/Rhino/Excel/Database/Future-Web → Application/Adapter → DataExchange/Repository interfaces → Core. Never the reverse.

## 5. AxisVar freeze
All ProgesiAxisVariable work is frozen. The frozen areas (do not modify, delete, consolidate, move, or wire) include:
- `src/ProgesiCore/ProgesiAxisVariable.cs`
- `src/ProgesiCore/ProgesiAxisVariableDto.cs`
- `src/ProgesiCore/Persistence/ProgesiAxisVariableRepository.cs`
- `src/ProgesiCore/Persistence/ProgesiAxisVariableSql.cs`
- axis-related DTOs in `src/ProgesiDataExchange/`
- `src/ProgesiGrasshopperAssembly/Components/AxisVarDefineComponent.cs`
- `src/ProgesiGrasshopperAssembly/Components/AxisVarSeriesComponent.cs`
- `src/ProgesiGrasshopperAssembly/Infrastructure/AxisVar/*` (AxisContext, AxisVarMapping, RhinoAxisStationing)
- axis-related tests

Read-only inspection and documentation are allowed. The freeze lifts only after the relevant ADRs are accepted and Gianluca explicitly authorises source changes.

## 6. Cursor boundary
- Cursor is **read-only** unless explicitly approved.
- Claude prepares Cursor task briefs **only when instructed**.
- Cursor implementation requires: an approved Task Board row, a branch, allowed files, forbidden files, tests, manual validation if Grasshopper is affected, and a rollback plan.
- Claude reviews Cursor output (diff + test results) before any merge or documentation update.
- Cursor smoke test passed, but Cursor still requires an explicit task brief and human approval before any implementation.
- **Tab / lane boundary (recorded 2026-07-29, relaxation adopted same day; canonical detail in the Notion "Ways of Working — 4-Tab Interface" page).** The four tabs are **01 Claude Main/Orchestrator · 02 Cursor Bridge · 03 Build-Test-Git · 04 ManualGH Validation**. Cursor implements on the branch in **02 Cursor Bridge and then STOPS**; Cursor must **not** run the tab-03/tab-04 lanes and must **not** edit lane or guard scripts — if a lane guard false-positives, Cursor **reports it and stops** (Claude, the lane/guard owner, fixes it). **Claude MAY prepare AND run the tab-03 (build/test/PR + merge) and tab-04 (deploy) lanes directly, gated by a lightweight human authorisation (a simple "go" / "move on" / "merge #N"); a full Human-Input decision row is required ONLY for Red decisions (authorising a code change / ADR / schema / branch-or-tag deletion / AxisVar).** Lane + guard scripts are owned by Claude; all git/PR operations run through prepared lane scripts, not ad-hoc commands.

## 7. Build/test commands
- Run build/test **only when explicitly instructed**.
- Canonical commands: `dotnet build -c Release` then `dotnet test`. Current operating baseline: **303 passing (+19 opt-in stress skipped) on `main` @ `0c2abf1`**. Historical: 230/230 at `d09130a` (Beta v0); 88/88 at `6286aec` (post-Cluster Phase 1); 64/64 at `376d81e`.
- Never run Rhino or Grasshopper from here.

## 8. Reporting requirements
After any action, report:
- changed files
- Notion updates made (if any)
- commands run
- test results (if any)
- `git status` (and branch/commit when repository interaction occurred)

## 9. Implementation prompt guard
If this prompt is running in Claude Code / 01. Claude Main/Orchestrator, stop immediately. Implementation may run only in 02. Cursor Bridge after Cursor Allowed = true.

- 01. Claude Main/Orchestrator must never execute implementation prompts.
- 02. Cursor Bridge must be a plain terminal by default.
- Cursor Agent implementation requires Cursor Allowed = true, an approved task brief, a branch, allowed files, forbidden files, tests, a rollback plan, and human approval.

## ProgesiVariableCluster Phase 1 recovery exception

ProgesiVariableCluster remains a missing capability / suspected regression and must not be treated as generally implemented.

Exception:
A human-approved, file-scoped Phase 1 recovery is authorised only on branch feat/cluster-recovery-portscope, only under the persisted "Cursor Task Brief v1.0 — ProgesiVariableCluster Recovery Phase 1 (file-scoped port)", and only after Cursor Allowed = true and the approved Task Board row is Ready for Cursor.

This exception does not authorise:
- AxisVar work
- wholesale branch merge
- Phase 2 SQLite recovery
- Phase 3 EF/DataExchange recovery
- source cleanup
- ADR acceptance
- branch/tag cleanup
- treating Cluster as fully implemented before build/test/manual validation and human review

AxisVar remains frozen.

Phase 2 and Phase 3 remain blocked until separately approved.

## Current operating baseline and status — reconciliation (2026-07-15)

This section reconciles the standing constraints and mode notes above with the current post-Cluster-Phase-1 / post-ADR-acceptance state. Where earlier text names `376d81e` / 64/64 as the *current* baseline, or describes the mode as "no-code handover", treat that wording as **historical**; the current operating state is recorded here. This section does **not** weaken any standing constraint, the AxisVar freeze, the implementation prompt guard (§9), or the ProgesiVariableCluster Phase 1 recovery exception above. It grants no new authorisation.

1. **Baselines.** *(Superseded 2026-07-23 — the current baseline is now `main` @ `d09130a`, 230/230; see the Post-Beta v0 reconciliation below.)* As recorded on 2026-07-15, the operating baseline was **88/88 tests passing at `6286aec`** (state after the PR #63 / Cluster Phase 1 merge), with the historical protected source-code checkpoint **64/64 at `376d81e`** on `feat/axis-variable-core`. §1 "Current mode" predates ADR acceptance and Cluster Phase 1 and is superseded by this section for current-state purposes.

2. **ProgesiVariableCluster.** Phase 1 is **recovered and closed** for the submitted and manually validated scenarios (Core model/service, InMemory repository, narrow Rhino support repository, ClusterDef/ClusterOut Grasshopper components, `ProgesiClusters` Excel export, Cluster tests; GH-CLUSTER-001..004 recorded Passed). **Phase 2 (SQLite persistence) and Phase 3 (EF / DataExchange) are not recovered and remain blocked** until separately approved. This is not a full release-validation sign-off. The Phase 1 recovery exception above remains in force exactly as written.

3. **ADR acceptance posture.** The three consolidation ADRs are now **Accepted as interim / direction-setting** — acceptance sets direction only and authorises no implementation:
   - **DataExchange ADR** — interim: keep DataExchange as the interchange boundary (Options A + E); long-term target is Option D.
   - **EF / SQLite ADR** — EF is the **long-term target**; the SQLite repository remains the **interim canonical** persistence; EF retirement is deferred and there is no near-term SQLite retirement.
   - **ProgesiDomainServices ADR (ADR-009)** — Option C direction accepted; consolidation is planned, not yet implemented.
   Any implementation flowing from these ADRs still requires an approved Task Board row, a branch, a task brief, tests, and human approval, and must run in 02. Cursor Bridge — never in 01. Claude Main/Orchestrator.

4. **AxisVar.** Remains **frozen and in abeyance** exactly as in §5. Nothing here lifts that freeze.

5. **Agents and Notion Curator.** All agents remain **controlled and human-gated** at their documented maturity levels (see `AGENTS.md`); no autonomous Task Board execution; implementation agents remain disabled; the Notion Curator operates only within its approved controlled-write scope.

6. **`main` and any beta/release line.** *(Superseded 2026-07-23.)* At the time of the 2026-07-15 reconciliation, `main` was untouched and this work was future-only. **This no longer holds:** Functional GH Beta v0 is now integrated into `main` at `d09130a` (via PR #72). See the Post-Beta v0 reconciliation section below.

7. **No new authorisation.** This section records state; it authorises no code cleanup, no ADR-driven implementation, no branch/tag cleanup, and no scope broadening.

## Post-Beta v0 reconciliation — 2026-07-23

This section records the current operating state after **Functional GH Beta v0** was integrated into `main`. It supersedes any earlier wording — including §1 "Current mode" and the 2026-07-15 reconciliation — that describes the mode as "no-code handover" or `main` as untouched/future-only. It does **not** weaken the AxisVar freeze, the implementation prompt guard (§9), the ProgesiVariableCluster Phase 1 recovery exception, or any authorisation gate. **It grants no new authorisation.**

1. **Baselines (retiered).**
   - **Current operating baseline:** `main` @ `d09130a` — Functional GH Beta v0 complete, **230/230 tests passing**, deployment succeeded.
   - **Historical baseline:** **88/88 at `6286aec`** — post-Cluster Phase 1 checkpoint.
   - **Historical protected source-code checkpoint:** **64/64 at `376d81e`** on `feat/axis-variable-core`.

2. **Current-State source of truth.** The Notion page **"Progesi Current State — Post Functional GH Beta v0"** is canonical. Future Claude sessions should start from that page, the active Task Board rows, the Architecture Map, the Roadmap, and `git status` — not from prior chat/session memory.

3. **Posture.** The earlier **no-code handover / governance setup** posture is **superseded**. The current posture is **post-beta consolidation / Claude handover / cleanup governance**.

4. **`main` status.** `main` is **no longer untouched**. Functional GH Beta v0 is integrated into `main` at `d09130a` (via PR #72). Any statement elsewhere in this file that `main` is untouched or future-only is historical and superseded by this section.

5. **GitHub / Notion cleanup.** Cleanup remains **audit-first**. Safe first passes and read-only audits have occurred (e.g. GitHub Cleanup Audit 366 and branch-protection settings verification 369A). **Destructive cleanup remains gated:** no branch/tag deletion, no Notion archive/delete/move, no ADR status change, no schema change, and no AxisVar work without explicit approval.

6. **Preserved.** The AxisVar freeze, the historical checkpoints (`6286aec`, `376d81e`), all ADR references and their acceptance posture, every human-approval/authorisation gate, and the protected/staged workflow language all remain in force exactly as written above.

7. **No new authorisation.** This section records state only. It authorises no code cleanup, no ADR-driven implementation, no branch/tag cleanup, no Notion deletion, and no scope broadening.

## Autonomous Operating Charter reconciliation — 2026-07-24

This section reconciles these operating rules with the **Autonomous Operating Charter & Standing Green Authorisation** (Notion, 08 — Governance and Tooling), created to let agents work with as little per-step ceremony as is safe while guaranteeing no information loss and safe rollback. It **does not weaken** any standing constraint, the AxisVar freeze (§5), §2's non-negotiable rules, the implementation prompt guard (§9), the ProgesiVariableCluster Phase 1 recovery exception, or any human-approval gate. **It grants no new authorisation.**

1. **Autonomy tiers.** Work is classified into three tiers:
   - **Green — pre-authorised (no per-step prompt).** Routine Notion hygiene (superseded notices, canonical pointers, archive/pointer organisation with no deletion), Current-State / Strategic-Planning-Log / change-log maintenance, and `{}` marker resolution. Green work is reversible via Notion version history and is always recorded in the change log.
   - **Amber — one explicit human go per scoped package.** E.g. the R1 GitHub-cleanup decision package, this governance-docs reconciliation, Task Board grouping.
   - **Red — explicit Human Input recorded before acting.** Branch/tag deletion, history rewrite, force-push, ADR status change, schema change, source-code change, Cursor implementation, and anything touching AxisVar. Red is never bundled; each Red action needs its own recorded decision.

2. **`{}` protocol (refined).** In addition to `{➕}` (human-added), `{⁉️}` (check currency), and `{⛔}` (Claude-raised block), the marker **`{@Claude …}`** is now supported: a **question** (Claude answers inline, appending `{answer: …}`, leaving the original text intact) or an **in-page action request** (e.g. `{@Claude move this section elsewhere}`). Green actions are executed, logged, and marked `{done: …}`; Amber/Red actions are converted to `{⛔}` and escalated for approval. Claude never deletes human content and never rewrites the original human wording; resolution is additive.

3. **No-information-loss guarantees.** Persist-then-archive, never delete; back up before any page-archive; child pages preserved as `<page>` blocks; mandatory dual change log (the Notion *ChatGPT Sync — Change Log* page and the on-disk `C:\Users\gianl\source\repos\ChatGPT_Sync_Change_Log.md` mirror, updated at the end of every controlled write).

4. **Rollback.** Notion version history for pages; Git branches/commits for any repository change (docs/rules only, on an approved docs/rules branch); additive/reversible Task Board schema (`Risk Tier`).

5. **Turn-end reminder.** A user-level Stop-hook (in `~/.claude/settings.json`, outside this repo) prints a change-log-update reminder when a turn ends inside the Progesi repository. It is a deterministic reminder only — it cannot compose the semantic change-log entry itself; that still requires an invoked Claude session. True recurring autonomy requires a separately approved scheduled driver.

6. **Preserved.** Everything above this section remains in force exactly as written.

7. **No new authorisation.** State and governance-model record only.

## feat/axis-variable-core checkpoint currency — 2026-07-29

This note reconciles the wording that describes the historical protected checkpoint as branch **`feat/axis-variable-core`** being *at* commit **`376d81e`** (see §1 line "Historical protected checkpoint: branch `feat/axis-variable-core` at commit `376d81e`", and the "64/64 at `376d81e` on `feat/axis-variable-core`" references in Standing constraints, §7, and the 2026-07-15 / Post-Beta v0 sections). It **does not** weaken the AxisVar freeze (§5), §2's non-negotiable rules, the implementation prompt guard (§9), or any gate, and grants **no new authorisation**. It corrects only the branch-tip wording.

1. **Checkpoint commit vs branch tip.** `376d81e` (clean tree, release build passing, 64/64 tests) is a historical **checkpoint commit** and remains valid. It is **not** the current tip of `feat/axis-variable-core`: the branch has since advanced to **`6d51987`** ("fix: guard Rhino-object Excel value round-trip"), with `376d81e` preserved as an ancestor in its history. Wherever earlier text implies "branch `feat/axis-variable-core` = `376d81e`", read it as **"checkpoint commit `376d81e`, within the history of `feat/axis-variable-core` (tip `6d51987`)"**.

2. **Fully merged + backed up.** The branch tip `6d51987` is an **ancestor of `main`** (it reached `main` via the Functional GH Beta v0 integration), so `feat/axis-variable-core` is fully merged — nothing on it is unmerged work. On **2026-07-29** the branch was **pushed to `origin`** for backup (non-destructive new remote ref; no force/history-rewrite; **no AxisVar code touched** — freeze intact). The checkpoint is therefore preserved three ways: the named ref on `origin`, within `main`'s history, and as the immutable commit `376d81e`.

3. **No change to the freeze or baselines.** The AxisVar freeze, every recorded baseline/checkpoint, and all authorisation gates remain exactly as written above. This note only clarifies that `feat/axis-variable-core`'s tip is `6d51987`, not `376d81e`.

## Baseline currency + tab/lane boundary reconciliation — 2026-07-29

Records the current operating state after the R2-C line and recent ADR dispositions. Supersedes any earlier "current baseline = `d09130a` / 230" wording (now demoted to a historical baseline). Does **not** weaken the AxisVar freeze (§5), §2's non-negotiable rules, the implementation prompt guard (§9), or any gate. **Grants no new authorisation.**

1. **Current operating baseline: `main @ 0c2abf1` — 303 tests passing (+19 opt-in stress skipped), 0 failed.** Reached via the R2-C line: R2-C.1 DataExchange extracted into the Rhino-free `Progesi.LiveDataExchange` (#86); SQLite geometry round-trip (#87); ClusterOut file-open fix (#88); EF geometry parity (#90); plus the docs correction (#89). Historical baselines retained: **230/230 at `d09130a`** (Functional GH Beta v0), **88/88 at `6286aec`** (post-Cluster Phase 1), **64/64 at `376d81e`** (protected source-code checkpoint). The canonical live baseline of record is the Notion **Progesi Current State** page + the **ChatGPT Sync — Change Log**.

2. **ADR dispositions (2026-07-29, by Gianluca).** **ADR-010 (canonical checkpoint = `376d81e`) → Superseded** — superseded by live operating-baseline tracking (above); `376d81e` is retained only as a historical checkpoint commit. **ADR-012 (ProgesiVariableCluster missing capability) → Superseded** — the premise is closed by the merged Cluster Phase 1 recovery (Phase 2/3 remain deferred). **ADR-014 (legacy removal requires documentation, validation and rollback) → Accepted** — now the governance policy of record; **any future legacy/dead-code removal must follow its doc + validation + rollback process** (e.g. R2-C.3 retirement of the dead `ProgesiDataExchange`).

3. **Tab / lane boundary (also added to §6).** Cursor implements in **05 Cursor Bridge and stops**; it does **not** run the tab-03 / tab-04 lanes or edit lane/guard scripts, and **reports guard false-positives rather than fixing-and-proceeding**. Claude owns the lane + guard scripts; git/PR operations run through prepared lanes (now the parameterised `Progesi-tab03-lane.ps1` / `Progesi-tab04-lane.ps1` + the merge lane), not ad-hoc commands. Full detail in the Notion "Ways of Working — 4-Tab Interface" page.

4. **No new authorisation.** State + governance-record only. AxisVar remains frozen.

## Tab renumber + lane-relaxation reconciliation — 2026-07-29

Records Gianluca's 2026-07-29 refinements on the Notion "Ways of Working — 4-Tab Interface" page. **Supersedes the tab numbering used earlier in this file** (§9's `00`/`05`; the `05 Cursor Bridge` mention in point 3 of the section above; and any `00. Controlled Writes`). Does **not** weaken the AxisVar freeze (§5), §2's rules, or the §9 implementation-prompt guard. **Grants no new source-code/ADR/AxisVar authority.**

1. **Canonical tabs:** **01 Claude Main/Orchestrator · 02 Cursor Bridge · 03 Build-Test-Git · 04 ManualGH Validation** (previously 00/05/03/04). §9 and §6 are updated to this scheme.

2. **Lane relaxation (adopted).** Claude MAY prepare **and run** the tab-03 (build/test/PR + merge) and tab-04 (deploy) lanes directly — including performing merges — gated by a **lightweight human authorisation** (a simple "go" / "move on" / "merge #N"). A full **Human-Input decision row remains required ONLY for Red decisions**: authorising a source-code change, an ADR status change, a schema change, a branch/tag deletion, or anything touching AxisVar. **Cursor still must NOT run any lane or edit lane/guard scripts**, and reports guard false-positives + stops. Git/PR operations run through the prepared parameterised lanes (`Progesi-tab03-lane.ps1`, `Progesi-tab04-lane.ps1`, `Progesi-tab03-merge-pr.ps1`), not ad-hoc commands.

3. **Unchanged:** the non-negotiable rules (§2), the AxisVar freeze (§5), the implementation-prompt guard (§9), and all Red gates remain in force. The relaxation concerns only *who may run the already-prepared lanes* and the *weight of authorisation for routine lane runs* — not what may be changed.

## Cluster Phase 2 (SQLite) authorisation + Phase 3 (EF) sequencing — 2026-07-29

Records Gianluca's 2026-07-29 explicit **Red decision** ("fire cluster P2" + "lift the block") lifting the ProgesiVariableCluster **Phase 2 (SQLite)** implementation block. Flows from the **Accepted tiered Persistence ADR (Option C)** — direct SQLite in the Rhino/GH tier, EF in the ASP.NET tier, one shared canonical schema + repository abstraction. Does **not** weaken the AxisVar freeze (§5), §2's non-negotiable rules, or the §9 implementation-prompt guard. Implementation still runs **only in 02 Cursor Bridge** (Cursor implements; Claude prepares briefs, runs the tab-03/04 lanes, reviews and merges).

1. **Phase 2 (SQLite) — UNBLOCKED / authorised.** The standing constraint and the ProgesiVariableCluster Phase 1 recovery exception — which stated "Phase 2 (SQLite) … remain blocked until separately approved" — are **superseded for Phase 2** by this dated authorisation. Scope (this Red decision covers the code change **and** the schema addition): add a `SqliteClusterRepository` implementing the **existing** `IProgesiVariableClusterRepository` (interface unchanged), mirroring the `SqliteVariableRepository`/`SqliteMetadataRepository` pattern (WAL, retry, ContentHash dedup, schema-evolution helpers), plus a new `Clusters` table and SQLite cluster unit tests. Governed by the persisted **"Cursor Task Brief v1.0 — ProgesiVariableCluster Phase 2 (SQLite)"**. Branch `feat/cluster-sqlite-phase2` off `main`; Task Board row Ready for Cursor / Cursor Allowed = true.

2. **Phase 3 (EF + SQLite↔EF parity) — authorised in principle, sequenced.** Authorised under the same decision but **fires only after Phase 2 merges** (the parity suite asserts against the merged SQLite cluster repo). Adds `ClusterEntity` + DbSet + model config + an EF migration + the SQLite↔EF cluster parity suite. Governed by **"Cursor Task Brief v1.0 — ProgesiVariableCluster Phase 3 (EF)"**. Branch `feat/cluster-ef-phase3`.

3. **Unchanged / still blocked.** AxisVar remains **frozen** (§5). R2-C.3 (retire dead Representation-A `ProgesiDataExchange`) remains **AxisVar-blocked**. The §9 guard and every other authorisation gate remain in force. This authorisation is scoped strictly to ProgesiVariableCluster Phase 2/3 persistence — it grants no other source-code, ADR, AxisVar, or cleanup authority.

## Cluster Phase 2/3 completion + baseline currency — 2026-07-29 (post-merge)

Records the completion of the ProgesiVariableCluster SQLite + EF persistence line and refreshes the operating baseline. **Supersedes** every earlier statement that Cluster Phase 2/3 are "not recovered" / "remain blocked" — specifically the Standing-constraints line ("Phase 2 (SQLite) and Phase 3 (EF/DataExchange) are not recovered and remain blocked"), the matching wording in the **ProgesiVariableCluster Phase 1 recovery exception** and the 2026-07-15 reconciliation (point 2), and demotes the earlier "current baseline `0c2abf1`/303" and `573cf0f`/314 lines to historical. Does **not** weaken the AxisVar freeze (§5), §2's non-negotiable rules, or the §9 implementation-prompt guard. **Grants no new authorisation.**

1. **Cluster persistence line — COMPLETE.** All three phases are done and merged to `main`: **Phase 1** (recovery, historical), **Phase 2 (SQLite)** — `SqliteClusterRepository` + tests (PR #97), and **Phase 3 (EF + SQLite↔EF parity)** — `EfClusterRepository` + `ClusterEntity` + `R2C3_AddClustersTable` migration + the cross-provider parity suite (PR #98). Clusters now persist through the same tiered pattern as Variables/Metadata (direct SQLite in the Rhino/GH tier, EF in the web tier), with conformance parity enforced. The "Phase 2/3 blocked/not-recovered" wording elsewhere in this file is therefore **historical**.

2. **Current operating baseline: `main @ 2d06035` — 335 tests passing (+19 opt-in stress skipped), 0 failed.** Reached via the cluster line (#97 SQLite → `dcc6585`/323; #98 EF+parity → `2d06035`/335) on top of the v1.1.0 release (tagged at `573cf0f`, 314 tests) and the #96 block-lift docs merge (`752d2d6`). Historical baselines retained: **314 at `573cf0f`** (v1.1.0), **303 at `0c2abf1`**, **230 at `d09130a`**, **88 at `6286aec`**, **64 at `376d81e`**. The canonical live baseline of record remains the Notion **Progesi Current State** page + the **ChatGPT Sync — Change Log**.

3. **Release.** **v1.1.0** is the current published GitHub release (tag `v1.1.0 → 573cf0f`, latest/stable). The cluster persistence added since is unreleased on `main @ 2d06035` (a future `v1.2.0` is a Red decision, not yet taken).

4. **Unchanged / still frozen.** AxisVar remains **frozen** (§5). R2-C.3 (retire dead Representation-A `ProgesiDataExchange`) remains **AxisVar-blocked**. The Persistence ADR stays **Accepted** (Option C). The §9 guard and every other authorisation gate remain in force. This note is a state/currency record only.

## ProgesiAxisVariable UNFREEZE — 2026-07-30

Records Gianluca's 2026-07-30 explicit **Red decision to UNFREEZE ProgesiAxisVariable**. **Both §5 conditions are now met:** **ADR-008** (persistence boundary) and **ADR-011** (canonical model + DTO) are **Accepted**, AND Gianluca has explicitly authorised source changes ("Unfreeze AxisVar"). **This SUPERSEDES the AxisVar freeze — the "AxisVar remains frozen / in abeyance" wording in the Standing constraints, §2, §5, and every dated reconciliation above — for the scoped ADR-008/011 evolution defined below.** It does **not** weaken §2's other non-negotiable rules, the §9 implementation-prompt guard, ProgesiCore's Rhino/EF/UI/ASP.NET-free rule (§4), or any other gate. Implementation runs **only in 02 Cursor Bridge** (Cursor implements; Claude prepares briefs, runs the tab-03/04 lanes, reviews + merges), on a **bespoke branch**, each package **Red-gated** (brief + branch + tests + review + manual GH where GH-coupled).

1. **Freeze LIFTED for the ADR-008/011 evolution.** The previously-frozen AxisVar files (`ProgesiAxisVariable.cs`, `ProgesiAxisVariableDto.cs`, `src/ProgesiCore/Persistence/ProgesiAxisVariable*.cs`, `AxisVarDefineComponent.cs`, `AxisVarSeriesComponent.cs`, `Infrastructure/AxisVar/*`, axis DTOs in `src/ProgesiDataExchange/`, and axis tests) may now be **modified under the accepted ADR-008 + ADR-011**, on a bespoke branch, with tests + review, and — for GH-coupled parts — mandatory manual GH validation. §5's blanket freeze no longer applies to this scoped work.

2. **Scope + sequencing (each package separately Red-gated):**
   - **B1** — ProgesiCore model evolution: evolve `ProgesiAxisVariable` (add CurvePayload / Mode / KeyPoints / Function / ContentHash, preserving the proven Rhino-free VO + no-dup normalized station map + Name/ValueType invariant + tolerance bucketing) + new first-class **`ProgesiFunction`** (deterministic expression engine, payload-serialized) + unify the Core DTO. CI-validated. Branch `feat/axisvar-unfreeze-b1`.
   - **B2** — relocate persistence out of Core → tiered `Sqlite/EfAxisVariableRepository` + a SQLite↔EF parity suite; **retire the in-Core ADO.NET repo under ADR-014** (documentation + validation + rollback); reconcile the duplicate DataExchange axis DTO.
   - **B3** — AxisVar GH components (Define/Series rework + Offset/Spacing/Segment-variation/AxesCrossReferences/Table/2D-drawing-sync) + the accepted **linear → true curve arc-length** reparameterisation at the adapter; **mandatory manual GH no-regression validation**.
   - **→ then ProgesiSection** (SectionRules) builds on the finished AxisVar.

3. **Still in force.** No-regression is required (preserve invariants; update equality/hash tests deliberately). **ProgesiCore stays Rhino/EF/UI/ASP.NET-free (§4)** — the curve is a Rhino-free serialized payload in Core; all Rhino math at the adapter. Existing GH `ComponentGuid`s preserved (new components get new GUIDs). The §9 guard and every other authorisation gate remain. This unfreeze is scoped strictly to ProgesiAxisVariable per ADR-008/011 — it grants no other authority. **R2-C.3 (retire dead Representation-A `ProgesiDataExchange`) is re-evaluated only when B2 reaches the axis DTOs, under ADR-014.**
