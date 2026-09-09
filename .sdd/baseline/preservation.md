# Preservation record

Baseline revision: `253433f08949d788e9774e7b2cd02ea5206a7fd7`.

`inventory.json` records 57 original definitions, all existing feature artifact hashes, and 12 observed gate results. `originals.zip` preserves the original bytes. No application source, tests, or existing `.specs/` files are changed by this migration.

## Ownership

| Content | Maintained source | Runtime output / durable record |
|---|---|---|
| Project guidance | `.sdd/contracts/project.md` | `AGENTS.md`; Claude imports it through `CLAUDE.md` |
| Execution rules | `.sdd/contracts/execution.md` | Read by both runtimes and their workers |
| 16 workflow procedures and references | `.sdd/skills/` | `.claude/skills/`, `.agents/skills/` |
| Nine specialist roles | `.sdd/roles/` | `.claude/agents/`, `.codex/agents/` |
| Native invocation, tools, permissions | `.sdd/adapters/` | Runtime metadata and Claude settings |
| Mechanical gates and formatting | `.sdd/scripts/` | Explicit shared execution; legacy Claude script paths retained |
| Feature spec, plan, manifest, ledger | Existing workflow owners | Same `.specs/{feature}/` for either runtime |

All 15 dotted `theshop.*` Claude entry points retain their names and arguments. Their canonical IDs replace the dot with a hyphen. The original `graphify` skill remains a Claude alias for `graphify-windows`; the actual `graphify` CLI command is unchanged. `catalog.json` is the complete old-to-new mapping.

Spec and plan schemas, requirement IDs, state transitions, invocation restrictions, approval boundaries, layer ownership, literal API handoffs, retries, and independent review remain intact. Codex uses skill invocation metadata separately from native role definitions. Claude role models and tools remain in its adapter; Codex inherits its authorized model and resolves capabilities against its actual host.

## Deliberate portability changes

- Invocation and MCP labels are rendered into native definitions. Native references point to rendered templates, so generator tokens cannot leak into feature artifacts.
- Project instructions move to `AGENTS.md`; Claude imports them. Shared procedures name capabilities instead of requiring Claude's Task tool or a Unix shell.
- The design checker has a runtime-independent path interface. Claude alone decodes hook JSON and maps violations to hook exit code 2; direct calls retain exit code 1.
- Required checks run explicitly. Whole-diff formatting moves from Claude's PostToolUse hook to the orchestrator after all workers finish. This prevents parallel workers from changing each other's files. A legacy explicit formatter entry point remains.
- The orchestrator repeats affected verification after formatting. Workers explicitly run design checks on their owned production files before handoff.
- A broken constitution checklist reference now points to the two existing checklists. Obsolete assumptions about automatic Stop-hook formatting are corrected.
- Executable gate messages use canonical workflow IDs where an old message named a slash command. Gate logic remains unchanged. The 12 captured gate outputs and exit codes remain identical.

## Existing problems preserved

The `breadcrumbs` spec and plan gates already fail at baseline; the other ten captured checks pass. The migration reproduces those outcomes.

An independent forward check also found two pre-existing instruction conflicts: `manage-brands/plan.md` assigns Domain test edits while the Domain role forbids test writing; Web resource-string instructions conflict with the implementation orchestrator's Application-only resource ownership. Affected workflows must report these conflicts before conflicting writes. They require a separate workflow/plan decision and are not silently resolved by adapter generation.

This migration adds no Caveman compression, routing changes, cloud telemetry, new credential configuration, or feature-schema changes. Those remain later phases.
