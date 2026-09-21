# The Shop SDD

Canonical SDD instructions live in `.sdd/`. Root `AGENTS.md` and `CLAUDE.md` delegate here.

## Layout

- `skills/`: portable workflow and project-governance skills.
- `scripts/`: deterministic gates and Stop-hook scripts.

## Workflow

`$theshop-start` → `$theshop-spec` → `$theshop-clarify` → `$theshop-plan` → `$theshop-resolve` → `$theshop-execute` → `$theshop-test` → `$theshop-verify` → `$theshop-ship`.

`$theshop-document` is optional. Review is not an SDD stage.

Feature artifacts remain in `.specs/{feature}/`.

## Artifact writing style

Write SDD artifacts using the active session writing style.

If Caveman mode is active, use its current level (`lite`, `full`, `ultra`, or `wenyan-*`). Otherwise, use concise normal prose. This project rule overrides any general Caveman rule that persisted documents use normal prose.

Style affects prose only. Preserve templates, headings, IDs, keywords, status values, code, commands, SQL, and `Given`/`When`/`Then` structure exactly.

## Skills

`theshop-clarify`, `theshop-constitution`, `theshop-document`, `theshop-e2e`, `theshop-execute`, `theshop-plan`, `theshop-resolve`, `theshop-ship`, `theshop-spec`, `theshop-start`, `theshop-test`, `theshop-verify`.

## Hooks

Both clients bind `.sdd/scripts/check-design-rules.ps1 -Changed` and `.sdd/scripts/format-on-stop.ps1` to `Stop`. Stop cannot match `Edit` or `Write`; both scripts no-op when no changed C# or Razor files exist.

Claude configuration: `.claude/settings.json`. Codex configuration: `.codex/hooks.json`. Review and trust newly discovered Codex hooks with `/hooks`.
