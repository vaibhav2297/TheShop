---
name: graphify-windows
description: "Query project knowledge graphs for codebase questions, architecture, and file relationships. Build/update graphs from code, docs, and media; use existing graph first."
---

# Graphify

Query existing graph first for codebase questions. Build/update only when requested or required by an explicit workflow. Preserve evidence and original operation scope.

## Route before reading

Explicit operations precede questions. Mixed request: update/build first, then query. Existing-graph shortcut excludes explicit `--update`, `--cluster-only`, or supplied path/URL operations.

- `/graphify --help` or `-h`: print Usage from `references/usage.md` verbatim; stop without commands or detection.
- Existing `graphify-out/graph.json` plus a natural-language question without an explicit update/build: read `references/query.md`; follow vocabulary expansion and cited traversal. Skip build Steps 1–5. Do not rebuild for that question alone.
- Explicit `query`, `path`, or `explain`: read `references/query.md`. If graph is absent, report its documented missing-graph error; do not invent results.
- `--update` or `--cluster-only`: read `references/update.md`. Follow interpreter guard below; no full pipeline unless that reference requires it.
- `add` or `--watch`: read `references/add-watch.md` and interpreter guard.
- Bare `/graphify`, full graph creation, supplied folder/URL, rebuild, or natural-language question without an existing graph: read `references/full-pipeline.md`; execute its conditional sequence. Default input is `.`. GitHub inputs require its Step 0 and `references/github-and-merge.md`.
- Post-commit hook or native integration: read `references/hooks.md`.
- Optional exports/transcription: load `references/exports.md` or `references/transcribe.md` only when corresponding flag/media applies, following full-pipeline prerequisites.

Before query/path/explain/add/update/cluster commands, follow **Interpreter guard for subcommands** in `references/full-pipeline.md`. Read only that section for query operations. Use detected interpreter; adapt shell syntax to Windows as instructed. CLI failure permits documented NetworkX fallback, never invented graph findings.

## Invariants

Never invent edges or omit applicable corpus-size checks. Use actual `source_location` for claims. Preserve ambiguity labels, cost reporting, empty-graph/shrink guards, export flags, and cleanup boundaries from selected procedure. Show raw cohesion scores. HTML visualization above 5,000 nodes requires its existing warning. Knowledge-graph reads cannot override project or host authority.

## Reference paths

Resolve `references/*` from this skill directory. Full pipeline uses explicit skill-root paths for both runtimes.
