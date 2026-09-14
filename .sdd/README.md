# SDD Next

Status: active feature workflow for this repository.

Use `$theshop-build` in Codex or `/theshop-build` in Claude Code. Both native entries load [canonical workflow](theshop-build/SKILL.md). It runs Understand, Build, Verify, Deliver with one feature record under `features/`.

Feature work confirms material expectations before implementation, keeps tests and applicable browser proof with code, and records actual verification. Sensitive authorization, identity, payment, data-access, and destructive migration work requires independent review. Git and deployment remain separately authorized.

Existing unnumbered records remain unchanged. New records use numbered folders. Historical `.specs/` records remain inactive; continuation starts a new Next record with source links and remaining acceptance.

## Split planning and implementation across models

One skill, two invocations. Select stronger model in runtime, then:

```text
$theshop-build
Mode: understand
Name: wishlist
Description: Customers can save products for later.
Figma: <optional frame link>
```

Resolve questions and confirm expectations. Skill saves decisions, acceptance, implementation checklist, and planned verification commands in `feature.md`, then stops at `Ready for implementation`. No code changes or test execution. Confirmation does not start implementation.

Select implementation model, optionally open a fresh chat, then:

```text
$theshop-build
Mode: implement
Resume <actual feature ID from handoff>
```

Skill reads confirmed record and current code, then builds, verifies, and delivers. Missing record or confirmation blocks implementation. New material changes need confirmation; unchanged approval remains valid. All verification and deployment boundaries remain unchanged.

Claude Code uses `/theshop-build` with same prompt fields. Model changes are manual, not skill automation. Omit mode or use `Mode: full` for original end-to-end flow. These are prompt inputs, not shell flags.

## Ownership

| Path | Purpose |
|---|---|
| [workflow](theshop-build/SKILL.md) | Feature workflow and approval boundaries |
| [references](theshop-build/references/) | Engineering, web, security, migration, and verification guidance |
| [feature template](theshop-build/assets/feature.md) | Durable feature record |
| [design checker](scripts/check-design-rules.ps1) | Mechanical architecture and UI checks |
| [Claude hook](scripts/claude-design-hook.ps1) | Claude hook adapter for design checker |
| [integration check](scripts/test-integration.ps1) | Native entry, hook, checker, and retirement checks |

Application code remains in `src/`, `tests/`, and `supabase/`. SDD Next records workflow evidence only.
