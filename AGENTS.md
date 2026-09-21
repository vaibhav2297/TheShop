# The Shop Instructions

Read `.sdd/README.md` first.

All project SDD instructions live in `.sdd/skills/`. Load `theshop-constitution` before code, test, Razor, SCSS, resource, or architecture work. Use `$theshop-*` skill names; do not use legacy `theshop.*` commands.

Feature artifacts live in `.specs/{feature}/`. Required flow: Start, Spec, Clarify, Plan, Resolve, Execute, Test, Verify, Ship. Document is optional. Review is not a workflow stage.

Do not duplicate, edit, or rely on workflow instructions outside `.sdd/`.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
