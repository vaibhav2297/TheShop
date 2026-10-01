# The Shop SDD

Canonical SDD instructions live in `.sdd/`. Root `AGENTS.md` and `CLAUDE.md` delegate here.

## Layout

- `skills/`: portable workflow and project-governance skills.
- `scripts/`: deterministic gates and Stop-hook scripts.

## Workflow

The storefront supports English only. Add user-facing text to `Strings.resx`. Do not add language-specific resources or locale switching. Earlier feature plans and reports that mention French are historical; this policy supersedes them.

`$theshop-start` → `$theshop-spec` → `$theshop-clarify` → `$theshop-plan` → `$theshop-resolve` → `$theshop-execute` → `$theshop-test` → `$theshop-verify` → `$theshop-ship`.

`$theshop-document` is optional. Review is not an SDD stage.

Feature artifacts remain in `.specs/{feature}/`.

New IDs: `NNN_feature-name`, e.g. `005_manage-product`.
Start chooses next number. Later stages preserve full ID. Legacy IDs stay valid.
Feature stages load [feature identity](skills/theshop-start/references/feature-identity.md).

## Artifact writing style

Write SDD artifacts using the active session writing style.

If Caveman mode is active, use its current level (`lite`, `full`, `ultra`, or `wenyan-*`). Otherwise, use concise normal prose. This project rule overrides any general Caveman rule that persisted documents use normal prose.

Style affects prose only. Preserve templates, headings, IDs, keywords, status values, code, commands, SQL, and `Given`/`When`/`Then` structure exactly.

## Skills

`theshop-clarify`, `theshop-constitution`, `theshop-document`, `theshop-e2e`, `theshop-execute`, `theshop-plan`, `theshop-resolve`, `theshop-ship`, `theshop-spec`, `theshop-start`, `theshop-test`, `theshop-verify`.

## UI fidelity

[Browser visual loop](skills/theshop-execute/references/visual-loop.md) activates only when a Figma design URL is supplied and recorded in feature plan.
With Figma: context + screenshots replace maintained design JSON; Execute renders, inspects and corrects; Verify/Ship require reviewed evidence.
Without Figma: normal SDD flow, including ordinary build, design-rule and behavioral checks. No visual scope, target table, screenshot evidence or waiver required.
Legacy plans with Figma URLs need visual scope and targets before next gate. Legacy plans without Figma remain valid.

## Agent adapters

Codex: `.agents/skills/theshop-*/SKILL.md`. Claude: `.claude/skills/theshop-*/SKILL.md`.
Adapters delegate to `.sdd/skills/`; workflow stays canonical. Pass arguments unchanged.
Resolve references/templates from canonical skill folder. Adapter prose: Caveman Ultra.
Preserve canonical invocation policy. Codex explicit-only policy lives in `agents/openai.yaml`.

## Hooks

Both clients bind `.sdd/scripts/check-design-rules.ps1 -Changed` and `.sdd/scripts/format-on-stop.ps1` to `Stop`. Stop cannot match `Edit` or `Write`; both scripts no-op when no changed C# or Razor files exist.

Claude configuration: `.claude/settings.json`. Codex configuration: `.codex/hooks.json`. Review and trust newly discovered Codex hooks with `/hooks`.
