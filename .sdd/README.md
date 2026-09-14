# SDD Next

Status: active feature workflow for this repository.

Use `$theshop-build` in Codex or `/theshop-build` in Claude Code. Both native entries load [canonical workflow](theshop-build/SKILL.md). It runs Understand, Build, Verify, Deliver with one feature record under `features/`.

Feature work confirms material expectations before implementation, keeps tests and applicable browser proof with code, and records actual verification. Sensitive authorization, identity, payment, data-access, and destructive migration work requires independent review. Git and deployment remain separately authorized.

Existing unnumbered records remain unchanged. New records use numbered folders. Historical `.specs/` records remain inactive; continuation starts a new Next record with source links and remaining acceptance.

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
