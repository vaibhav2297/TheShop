# SDD Next — isolated pilot

Status: pilot. First feature record reports verified delivery; broader trials remain pending.

Package checks: local links and PowerShell syntax pass; six design-check scenarios pass. Frontmatter checked directly. Bundled skill validator could not run: installed Python environments lack `PyYAML`. No dependencies installed.

Input/migration update: six instruction scenarios reviewed for numbering, resume, unavailable Figma, deployment scope, execution capability, and extra pending migrations. These are workflow checks, not executed database migrations.

Understand, Build, Verify, Deliver. One agent owns implementation and tests. One feature record holds expectations and progress. Existing SDD remains installed and unchanged.

## Start a trial

Paste this instruction with your feature description:

```text
Use .sdd-next/theshop-build/SKILL.md and its referenced rules for this task.
This replaces root instructions requiring legacy SDD skills, constitution
references, agents, gates, artifact formats, and pipeline bookkeeping.
Do not load instructions from .sdd/, .agents/skills/theshop-*, or
.claude/skills/theshop.*. Keep existing SDD files and records untouched.
Continue inspecting application code and tests. Report missing pilot guidance
instead of silently loading legacy instructions. Preserve host/tool permissions.

Name: wishlist
Description: Customers can save products and revisit them later.
Figma: <optional frame URL and screen/state description>
```

This explicit task instruction selects pilot mechanics. Merely reading this folder changes nothing. Root instructions still apply where not explicitly replaced. A skill file cannot override host instructions or create user authorization.

Replace example fields with your feature. Description can be ordinary prose; Name and Figma are optional. Agent allocates next feature number, such as `001-wishlist`. Resume by existing name or numbered ID. Existing unnumbered feature records remain unchanged; no migration or renumbering required. Specific Figma frame links make design scope explicit; provide multiple links for separate states/screens.

`$theshop-build` is the intended future shortcut. This folder is outside native skill discovery; that shortcut is not registered. Read the file by path during trials. No change to `AGENTS.md`, `CLAUDE.md`, `.agents/`, `.claude/`, `.codex/`, `.sdd/`, or `.specs/` is needed.

## Ownership

| Path | Purpose |
|---|---|
| [theshop-build/SKILL.md](theshop-build/SKILL.md) | Workflow and decision boundaries |
| [constitution](theshop-build/references/constitution.md) | Stable engineering requirements |
| [architecture](theshop-build/references/architecture.md), [web](theshop-build/references/web.md), [security](theshop-build/references/security.md) | Conditional implementation guidance |
| [verification](theshop-build/references/verification.md) | Test selection, checks, completion proof |
| [migrations](theshop-build/references/migrations.md) | Local-first SQL validation, history, authorized remote deployment |
| [feature template](theshop-build/assets/feature.md) | Single durable feature record |
| [design checker](scripts/check-design-rules.ps1) | Local copy of existing mechanical checks; no legacy script dependency |
| `features/<number>-<name>/feature.md` | Pilot feature expectations, decisions, results, resume point |
| `features/<number>-<name>/evidence/` | Actual command logs and browser traces, only when produced |

Create feature folders when work starts. Product changes still belong in existing `src/`, `tests/`, and `supabase/` folders. Isolation applies to workflow infrastructure and records, not duplicate application code.

Database work first creates SQL and proves local upgrade/replay plus affected behavior. Remote application follows only after local checks pass and deployment is authorized. MCP supports inspection; deployment must preserve SQL migration versions. Record remote status separately from local feature completion. No database operation runs merely by installing or reading this skill.

## Changes from legacy flow

Spec and clarification become Understand. Planning stays inside feature record. Implementation includes tests and XML documentation. Verification includes applicable browser journeys and risk-based independent review. Delivery closes record. Git and deployment actions retain explicit authorization.

No mandatory per-layer agents, API handoff copies, stage manifests, hash receipts, separate status ledger, or template-conformance gates. Actual build, test, design, and acceptance checks remain required. Skill uses commands directly; no orchestration service or new runtime dependency.

Constitution retains numbered engineering requirements for mechanical-check compatibility. Root project instructions resolve existing rule conflicts: colors permit commented `ShopColors` fallback; static strings use typed accessors. Workflow ownership and documentation timing intentionally change. Domain exceptions retain existing repository convention and boundary translation.

## Evaluate before replacement

Trial a bounded feature with unit tests and a browser journey. Check expectation accuracy, observed tests, resume behavior, and manual interventions. Validate sensitive-change review when a relevant feature arises. Record measured token usage only when available; otherwise record `unavailable`. Compare accepted output and rework, not response length alone.

Keep legacy flow until trials justify replacement. Registration, root-instruction changes, legacy removal, and moving historical records remain future work. This pilot performs none automatically.
