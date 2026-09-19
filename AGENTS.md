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

## Writing style

When caveman mode is active, apply its selected level to generated SDD specs such as `spec.md` or `plan.md` Markdown files.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
