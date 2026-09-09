# Caveman adoption

Scope: 16 existing workflows, 9 existing roles, Claude Code and Codex. Caveman only; no Cavekit grammar or lifecycle. No additional agents, proxy, cloud, telemetry, memory service, or model routing.

## Decisions implemented

- Official `full` default for all prose, including persisted artifacts; explicit selection for other levels.
- Upstream auto-clarity preserved; selected mode resumes afterward.
- Exact technical literals, conditions, exceptions, uncertainty, IDs, templates, and evidence preserved.
- One shared policy; every generated workflow/role explicitly loads it. Execution contract and native adapters carry active mode to workers.
- Read required sources once per context unless changed, missing, or needed again. No cache guarantee or lossy replacement of requirements.
- Compact handoffs within existing report fields; complete API signatures remain mandatory.

## Adoption matrix

| Workflows/roles | Change | Preservation evidence |
|---|---|---|
| All 16 workflows | Short discovery description; shared policy and compact generated bootstrap | Catalog identity/invocation parity; native discovery; policy reachability |
| All 9 roles | Short discovery description and purpose; mode/language handoff | Ownership/read-only parity; literal code-block preservation |
| Spec, Plan, Clarify, Resolve | Invocation examples moved to conditional references; concise introductory prose | Example content retained; templates unchanged; native artifact/decision cases |
| Graphify | Small operation router; full construction procedure moved to reference | Original procedure retained with native-renderable reference paths |
| Implement, Execute, Test, Test-merged | Concise orchestration introduction; shared context/handoff rules | Scope, retry, gate, and API requirements retained |
| Review, Document, Verify, E2E, Start, Ship | Concise purpose; existing procedures and approval boundaries | Existing executable examples and gates unchanged |
| Constitution | Concise discovery/purpose; numbered rules/references retained | Rules remain authoritative within host/user hierarchy |

Baseline: `baseline/caveman/phase5.zip` preserves 244 files, including original shared sources and 169 generated outputs. Candidate changes are authored selectively; no runtime text-rewriting engine. Code examples and verification scripts remain exact. Longer required procedures remain where shortening would need additional behavioral proof.

## Inherited conflicts

These predate Caveman. Compression does not grant permission to bypass them:

- Domain role forbids test writes; project/constitution require tests alongside new units. Existing manage-brands plan assigns a Domain test task.
- Web plan/role expects resource writes; orchestration assigns resource ownership differently.
- Review requires a ledger update before approval but also prohibits every pre-approval edit.
- Domain summary asks for guessed ambiguities while Domain workflow forbids guessing missing details.
- Test writer counts Theory data rows, then describes class totals as methods.
- Test roles prohibit constitution loading while project guidance requires it for test code work.
- Existing breadcrumbs spec/plan gates already fail. Phase 1–5 preservation report records those baseline results.

Surface relevant conflict with source evidence. Do not silently alter feature behavior, ownership, or approval rules to hide it.
