# Project instructions

Before feature work or other code changes, read:

1. [.specs/README.md](.specs/README.md) — the canonical SDD workflow.
2. [.specs/principles.md](.specs/principles.md) — the mandatory project architecture, design, testing, and documentation rules.

- Use `.specs/templates/spec.md` and `.specs/templates/plan.md` to create sibling `spec.md` and `plan.md` in `.specs/features/<feature-name>/` for meaningful features. Read both before implementing or resuming work.
- Keep requirements, principle references, exception approvals, acceptance criteria, and feature status in spec.md. Keep implementation tasks and verification/release evidence in plan.md. Reference IDs instead of duplicating requirements.
- Identify applicable principle IDs during specification, follow them during implementation, and record compliance evidence during verification. Tiny changes may skip a spec, but never applicable rules.
- Keep workflow instructions in the README and rule definitions in principles.md; do not duplicate them here.
- If a request, Figma design, or existing code conflicts with a mandatory rule, explain the conflict and obtain an explicit owner decision before implementing that portion. Continue unaffected work where practical. Record approved exceptions in the feature spec, or existing change notes for a tiny change.
- Never silently weaken principles, treat general feature approval as an exception, or claim unverified compliance. Follow the README's completion and release rules.
- If either required guide is missing or unreadable, report it and obtain the missing guidance before affected implementation; do not reconstruct rules from memory.

Preserve and follow other applicable repository instructions. These references do not authorize release actions or override higher-priority instructions or the owner's authority to explicitly amend project policy.
