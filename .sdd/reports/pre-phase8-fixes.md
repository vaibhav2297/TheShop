# Before phase 8 — duplicate discovery and commit precedence

Date: 2026-09-06. Fixes published locally and verified. Changes uncommitted.

## Fixed

- Extension skill wrappers now reference pinned `.sdd/extensions/packages/{id}/{revision}/upstream/` directly, matching agent wrappers. Vendor trees stay outside native skill-discovery roots. Nested vendor skills cannot become independent project entrypoints through generation.
- Retired six owned generated files: help's vendor `SKILL.md`, `LICENSE`, and vendor `.gitattributes` in each runtime. Original packages, hashes, relative vendor dependencies, licenses, and global Caveman installation remain unchanged.
- Project commit rule explicitly overrides helper formatting: `{Type} | {Description}`. Applies to direct message requests and `caveman-commit`; Ship reiterates precedence. Existing attribution prohibition and git confirmations remain intact.
- Documented same-revision manager update for generator-layout changes. Ordinary sync still refuses stale paths; ownership checks remain strict.

Publication used existing manager preview and journaled update against unchanged help revision `02de72366823a4939ea8fa823b161ac2b7e41f4509b6a255ad8b607cd2b794f1`. Transaction: `968b039f87eb43ebadb9cd9aa201874c`. Twelve file changes: six retired vendor copies/attributes, five regenerated instructions, one generation manifest. Registry and retained packages unchanged.

## Evidence

- Regression reproduced before fix: vendor tree appeared under `.agents/skills/extension-probe`.
- Isolated extension checks: 40/40 pass. Includes recursive discovery, original vendor/binary bytes, relative references, same-revision retirement, independent-edit rejection, unrelated-file preservation, update, rollback, recovery, and Git checkout.
- Isolated portability checks: 18/18 pass.
- Published adapter drift: 245 files clean.
- Git whitespace check: pass.
- Published extension checks: 44/44 pass, including both retained package pins and wrapper-only discovery in both runtimes. [Evidence](extension-static.json).
- Published Caveman checks: 10/10 pass. Combined with 18 portability checks, 72 scripted checks pass.
- Local AST graph refreshed. SQL extraction remains unavailable without existing missing `tree_sitter_sql`; no dependency installation or cloud labeling performed.

Generator correction does not rewrite upstream skills or introduce task routing. Native discovery assertions inspect generated files; no new authenticated client smoke test was run for this correction. Restart Claude Code/Codex sessions to refresh cached skill listings. Current conversation can retain old discovery metadata until restart.

Phase 7 semantic/style findings remain recorded separately. Phase 8 remains pending.
