# SDD Next migration plan

Status: Implemented locally; fresh runtime and remote CI/branch-protection proof pending.
Date: 2026-09-14
Inspected baseline: `36cec5c`, branch `refactor/sdd-flow`; no tracked changes before planning.

Outcome: `.sdd` is sole active feature workflow for Claude Code and Codex. Retire legacy orchestration, native registrations, agents, scripts, hooks, CI, and instructions. Preserve engineering requirements, application behavior, feature history, and unrelated integrations.

Migration requested on 2026-09-14. The local cutover removed legacy tracked and ignored workflow files, registered Next entries, restored the unique historical `shop-image` record, and refreshed Graphify. No commit, push, pull request, deployment, or remote configuration change occurred.

## 1. Observed state

| Surface | Evidence | Migration implication |
|---|---|---|
| New workflow | [README](README.md), [skill](theshop-build/SKILL.md) | Pilot only; native `theshop-build` shortcut absent. Entry prompt temporarily overrides legacy instructions. |
| New verification | [verification](theshop-build/references/verification.md), [checker](scripts/check-design-rules.ps1) | Direct formatting/build/tests/browser proof; checker already independent of legacy scripts. |
| Pilot delivery | [reusable-image-treatments](features/reusable-image-treatments/feature.md) | Record reports Done, 152 focused tests and 3 E2E tests passed. Historical evidence; not rerun during planning. |
| Legacy source | `.sdd/catalog.json`, `.sdd/generated-files.json`, `.sdd/contracts/`, `.sdd/skills/`, `.sdd/roles/` | 246 tracked files under `.sdd`; generated manifest assists ownership inventory, not blanket deletion. |
| Native registrations | `.agents/skills/`, `.claude/skills/`, `.claude/commands/`, `.claude/agents/`, `.codex/agents/` | Claude has dotted compatibility names and hyphenated names. Both runtimes have 10 project agents each. |
| Root instructions | `AGENTS.md`, `CLAUDE.md` | Generated from `.sdd/contracts/project.md`; require old pipeline, constitution, formatting, gates. |
| Claude hook | `.claude/settings.json`, `.claude/scripts/check-design-rules.ps1` | Hook wrapper searches for `.sdd/catalog.json`, then invokes old checker. Directly changing checker path alone leaves root discovery broken. |
| CI | `.github/workflows/sdd-portability.yml` | Five checks invoke old scripts. Trigger paths omit active SDD files. |
| History | `.specs/`: 12 folders, 11 status files | Ten status files include shipped markers; all 11 contain waivers. `authentication` lacks shipped marker; `e2e-test` lacks status file. Inspect before deciding continuation. |
| Existing references | Test XML documentation links `.specs/.../spec.md` | Preserve history paths; mass rewriting adds unrelated churn. Focused scan found no `.sdd/` execution reference in `src/` or `tests/`. |
| Other leftovers | `.sdd-temp-snapshot/`, `.claude/settings.json.graphify-bak`, `update-skill.ps1` | Snapshot has 83 tracked files, including application/tests/SQL/history copies. Old updater expects absent root architecture/design files. Inventory before removal. |
| Graphify | Both native copies load legacy execution contracts; `.claude/CLAUDE.md` points to `graphify` | Preserve graph capability; remove dependency on legacy orchestration and resolve duplicate registrations. |

Graph query used existing vocabulary: `sdd claude codex adapter skill workflow migration`. Returned root contracts, adapters, generator, evidence scripts, and Next security reference. Direct reads establish details above.

## 2. Target decisions

- Keep `.sdd` name. Make production status explicit; remove pilot selection and override ceremony.
- One canonical workflow: `.sdd/theshop-build/SKILL.md`. Understand, Build, Verify, Deliver; one implementing agent and one feature record.
- Codex entry: `$theshop-build`; Claude entry: `/theshop-build`. Natural-language feature requests route here through root instructions. Questions and unrelated tasks retain ordinary handling.
- Add thin native entry files at `.agents/skills/theshop-build/SKILL.md` and `.claude/skills/theshop-build/SKILL.md`. Each loads canonical skill by repository path; supporting references resolve from canonical directory.
- Native entries contain discovery metadata and loading instructions only. No copied workflow, symlink requirement, adapter generator, catalog, or replacement orchestration framework.
- Make `AGENTS.md` authoritative for shared project instructions. Root `CLAUDE.md` explicitly loads it. Keep Claude-specific instructions minimal; prove loading from root and nested working directories.
- Root instructions preserve engineering rules, `{Type} | {Description}`, no attribution, Graphify policy, and Caveman default `full` with explicit overrides. This session's `ultra` remains session preference.
- Keep Next's conditional independent review. Remove legacy specialist agents; use available generic reviewer with complete scoped review instructions. Missing independent capability leaves review pending.
- Keep existing unnumbered pilot record unchanged. New records use existing numbered allocation; no renumbering or automatic history conversion.
- Retain `.specs/` as inactive historical records. New writes use `.sdd/features/`; continuing old work creates one new record with source links and explicit remaining acceptance.
- Preserve unrelated Graphify/Caveman tools, personal skills, plugins, MCP connections, credentials, application code, tests, and SQL migrations.

Native registration locations and discovery behavior: [Codex skills](https://learn.chatgpt.com/docs/build-skills), [Claude skills](https://code.claude.com/docs/en/skills). Codex project agent files live under `.codex/agents/`: [Codex subagents](https://learn.chatgpt.com/docs/agent-configuration/subagents).

## 3. Execution sequence

### Phase A — Inventory and recovery baseline

1. Recheck revision, branch, dirty files, ignored files, running feature sessions, and active CI. Pause workflow writers during cutover.
2. Record exact legacy-owned paths from catalog, generated manifest, extension registrations, and filesystem. Include dotted aliases and untracked/local hooks.
3. Inventory `.sdd/.stage`, `.sdd/.test-work`, baseline archives, reports, and `.sdd-temp-snapshot`. Compare snapshot files with live counterparts; preserve unique work before contraction.
4. Record recoverable Git baseline for tracked files. Preserve necessary untracked content in a validated backup outside native discovery. Never assume Git contains ignored evidence or machine-local settings.
5. Inspect project-scoped Claude local settings, Git hooks, Codex settings, and discoverable personal registrations for old path references. Report key/path matches only; never dump credentials or entire personal configuration.
6. List external settings requiring later action, including protected-branch required checks. No global skill removal unless a specific installation is proven project-owned and removal is authorized.

Exit: explicit keep/replace/remove inventory; recovery locations verified; unique data accounted for. No wildcard deletion against native directories.

### Phase B — Complete standalone guidance

1. Promote Next README and skill descriptions from pilot to active workflow. Remove instructions to retain old flow and trial-only entry requirements.
2. Compare all ten root non-negotiable rules and old constitution checklists with Next references. Preserve engineering constraints; explicitly retire stage mechanics, separate documentation stage, and mandatory per-layer handoffs.
3. Move any still-required communication or operating guidance into root instructions or focused Next references. No active instruction may require `.sdd/` to exist.
4. Finalize root routing: ordinary feature requests use Next; planning-only requests stop after plan; existing expectation approval remains valid. Git and remote actions require their existing explicit authorization.
5. Detach Graphify native skills from `.sdd` execution/adapters and generator provenance. Preserve Windows interpreter guidance, query/update behavior, and unrelated Graphify hooks. Select one Claude registration; update `.claude/CLAUDE.md` accordingly.
6. Describe old-feature continuation and evidence freshness in Next resume guidance. Keep historical waivers and failed acceptance visible.

Exit: required guidance resolves without legacy reads; engineering-rule comparison records no unexplained loss.

### Phase C — Register runtimes and replace automation

1. Add two thin `theshop-build` native entries. Use same workflow name, scope, canonical source, and feature directory. Avoid duplicate command aliases.
2. Replace root instructions in same cutover change. Remove generated-source banners and old command pipeline tables. No transition where new shortcut still mandates old gates.
3. Replace Claude design-hook wrapper with `.sdd/scripts/claude-design-hook.ps1`; point settings there. Resolve root through SDD path, decode hook JSON, invoke shared checker, preserve failure feedback.
4. Keep explicit checker invocation for Codex and final verification for both runtimes. Hooks supplement checks; no hook dependency for completion. Validate Windows absolute paths and spaces against [Claude hook input](https://code.claude.com/docs/en/hooks).
5. Remove obsolete formatter/gate hook registrations from shared and project-local settings when present. Preserve unrelated hook entries and permissions. Next uses scoped `dotnet format` directly.
6. Replace legacy CI with `.github/workflows/sdd.yml`. Add `.sdd/**`, root instructions, and relevant native paths to triggers.
7. Add one focused validation script, `.sdd/scripts/test-integration.ps1`, for native entries, local links, syntax, hook fixtures, checker fixtures, and forbidden active legacy references. No per-feature manifest or ledger gate.
8. Verify branch protection requirements before renaming required check identities. Preserve existing required identity until authorized remote settings are updated; avoid PRs waiting permanently for deleted jobs.

Exit: clean fixture checkout can discover Next in both runtimes and run validation with legacy tree absent. Scripts above are proposed; none created or run during planning.

### Phase D — Preserve history and discontinue legacy files

1. Add `.specs/README.md` marking records historical and defining continuation links. Leave existing specs, plans, statuses, test manifests, evidence, and test traits intact.
2. Inventory unresolved records without inferring active work from missing shipped markers. Transfer only user-selected continuation into Next; retain original acceptance IDs in source mapping. Never convert waivers into passes.
3. Remove all inventoried old workflow skills from `.agents/skills/` and `.claude/skills/`, including dotted compatibility copies. Remove old `.claude/commands/theshop.*` entries.
4. Remove nine legacy `shop-*` agent definitions plus `sdd-cavecrew-reviewer` from each runtime. Remove generated `sdd-caveman-help` skills and old extension registrations. Generic personal Caveman tools remain.
5. Remove old `.claude/scripts/check-sdd-gates.ps1`, `format-on-stop.ps1`, and replaced design wrapper after settings use Next.
6. Remove `.sdd/` after required rules, evidence, and recovery material are preserved. Remove obsolete CI file and update `.gitignore` entries for old staging/extensions; add narrow Next temporary/evidence exclusions where needed.
7. Remove `.sdd-temp-snapshot/`, `.claude/settings.json.graphify-bak`, and obsolete `update-skill.ps1` only after inventory proves recovery and no remaining consumer. These are separately listed deletion targets, not automatic cleanup by folder name.
8. Inspect `skills-lock.json`; remove only entries tied solely to removed project installation. Keep entries for retained independent tools.
9. Resolve every deletion target to an absolute path within named repository subtrees. Delete only manifest-listed paths; reject unknown new files. Repeated execution reports already-removed paths safely.

Exit: no active legacy installation. Historical references remain explicitly classified; no default routing to historical instructions.

### Phase E — Prove cutover and close

Run matrix below after contraction, from clean checkout and fresh runtime sessions. Record actual commands, versions, exits, counts, and transcript/evidence paths. Missing runtime access is pending proof, not success.

Refresh Graphify once after migration; verify old active-node recommendations no longer route to deleted files. Graph refresh is derived-index maintenance, not workflow acceptance proof.

Summarize changed entry points, deleted targets, preserved history, recovery baseline, and outstanding machine-local actions. Stop when matrix passes. Commit/push/PR/deployment occur only when separately requested.

## 4. Verification matrix

| Check | Required result |
|---|---|
| Static registration | Exactly one `theshop-build` entry per runtime; canonical path and reference links resolve. Root/nested instruction loading verified. |
| Native discovery | Fresh Codex `$theshop-build` and Claude `/theshop-build` load Next. Removed project skills/agents absent. Capture origin if personal/plugin duplicate appears. |
| Routing | Natural-language feature request selects Next; planning-only request does not implement; unrelated question does not create feature record. |
| No legacy dependency | Validate with `.sdd` and old native files absent. No active root, skill, hook, script, CI, or local registration references old execution paths. |
| Checker and hook | Valid file passes; intentional rule violation fails; missing path fails; malformed/unrelated hook input handled; Windows paths/spaces work; hook propagates actual checker failure. |
| Workflow boundaries | Already-confirmed expectations do not trigger repeated approval; unresolved material decision pauses dependent work; shipping/deployment remain unauthorized unless requested. |
| Feature identity | New numbered record, existing unnumbered resume, ambiguous name, missing resume, and occupied number handled without overwrite. |
| Cross-runtime resume | Claude creates record, Codex resumes; reverse direction also tested. Acceptance and valid evidence survive. One record writer at a time. |
| Evidence honesty | Failed build/test, zero tests, unavailable browser, missing review, and stale evidence cannot produce Done. Local and remote migration status remain separate. |
| Independent review | Sensitive fixture requests bounded independent review; unavailable reviewer leaves pending proof. No legacy role files loaded. |
| Preservation | Existing `.specs` records and Next pilot record unchanged; snapshot unique content recovered; application/tests/SQL unchanged unless explicit migration necessity documented. |
| CI | New validation passes on pull-request fixture; legacy commands absent; required-check identities remain satisfiable. Azure app deployment workflows preserved. |
| Repeatability and rollback | Repeated validation passes; interrupted cutover cannot silently pass; disposable rollback restores old discovery/settings/CI without deleting Next feature records. |

Use small disposable fixture records for runtime scenarios. Do not mutate completed feature history to test resume. Existing pilot delivery supports design confidence but does not prove native runtime registration.

No application behavior change expected. Run focused integration/checker fixtures and link/config checks. Run solution build if checker behavior, application-facing files, or test infrastructure changes. Do not rerun every product E2E solely for workflow-file removal.

## 5. Rollback

Trigger: native discovery failure, lost engineering requirement, broken required CI, unreadable feature history, or hook failures introduced by cutover.

1. Stop workflow writers. Preserve new Next records and evidence created since cutover.
2. Restore inventoried legacy files, root instructions, native registrations, shared/local hook settings, and CI from recorded baseline/backup. Restore only migration-owned changes; preserve concurrent work.
3. Remove or disable only new Next native entry registrations. Keep canonical Next package and feature records for diagnosis.
4. Restore required-check configuration if changed, using its authorized recovery procedure. Restart both runtimes; verify legacy discovery and gate targets resolve.
5. Record failed phase and correction before retry. Do not translate Next completion automatically into legacy ledger success.

No `git reset --hard`, repository-wide restore, blanket user-profile deletion, or remote-history rewrite. Git history preserves tracked legacy implementation after retirement; unique local content requires explicit backup.

## 6. Completion checklist

- [x] Inventory and recovery baseline verified.
- [x] Next guidance complete; preserved engineering rules mapped.
- [x] Both native entries load same canonical workflow by static integration check.
- [x] Root instructions contain no active legacy requirements.
- [x] Graphify works independently; derived index refreshed.
- [x] Old skills, aliases, agents, hooks, scripts, CI, snapshot, and ignored legacy workspaces removed by exact inventory.
- [x] Historical records preserved; old-feature continuation documented.
- [ ] Ignored local Claude settings and external branch-protection requirements remain outside repository authority.
- [ ] Fresh Claude and Codex sessions must prove native discovery and cross-runtime resume.
- [x] Final local active-reference scan and integration check pass; hosted CI remains pending.

Recommended implementation order: recovery/inventory; standalone guidance and native registration; automation replacement; verified contraction; fresh-session proof. Publish one coherent cutover so neither runtime sees a partially migrated default workflow.
