# Release operation

Release status lives in `reports/release-readiness.md`. Static checks, native behavior, and real feature proof remain separate claims.

## Install

Use a reviewed repository revision containing `.sdd/`, generated `.claude/`, `.agents/skills/`, `.codex/agents/`, `AGENTS.md`, and `CLAUDE.md`. Keep `.sdd/baseline/` and extension source pins: verification and controlled rollback need them. This is The Shop's project workflow, not a generic drop-in package for unrelated applications.

Required for generation/checks: Git and PowerShell 7. Required for implementation/tests: project .NET SDK and restored project dependencies. Native workflows additionally need authenticated Claude Code or Codex, available quota, and capabilities listed in the active adapter. Browser/database proof needs local Docker, Supabase, and Playwright prerequisites. Never treat unavailable tooling or skipped tests as passing.

From repository root:

```powershell
pwsh -NoProfile -File .sdd/scripts/sync-adapters.ps1 -Check
pwsh -NoProfile -File .sdd/scripts/test-portability.ps1 -Published
pwsh -NoProfile -File .sdd/scripts/test-caveman.ps1 -Published
pwsh -NoProfile -File .sdd/scripts/test-extensions.ps1 -Published
pwsh -NoProfile -File .sdd/scripts/test-evidence.ps1
pwsh -NoProfile -File .sdd/scripts/test-release-contracts.ps1
```

Open the repository in the selected runtime and verify skill discovery. Existing upstream task helpers remain optional installations, audited by `check-skill-discovery.ps1`; no startup download or duplicate skill trees. Caveman `full` policy works through shared contracts. No cloud gateway or proxy required.

## Upgrade

Finish active workers first. Record current revision and preserve uncommitted work, `.specs/`, evidence history, and local settings. Review canonical changes and generated diff together. Edit shared sources only; run generator and checks above. Generator refuses independently edited owned output; reconcile that edit into canonical source before retrying.

Legacy feature ledgers stay readable. Establish hash-backed evidence through actual producing checks; never backfill success from old state labels. New stage receipts require successful check IDs, commands, exit codes, and retained nonempty logs. Resolve failures by explicit failure IDs linked to successful checks. Tests support stage-specific deferred browser/human proof; see `contracts/test-proof.md`.

Run native representative workflows on each supported runtime before promoting changed behavior. Keep model, source revision, logs, artifact review, and quota limits visible. Broader execution-strategy benchmarks remain optional while defaults stay unchanged.

## Clean candidate validation

`prepare-release-fixture.ps1` copies tracked files plus intended SDD additions into an isolated local Git checkout. Excludes ignored files, environment files, build output, and graph output. Main checkout stays untouched. It records source hashes and clean fixture revision in `reports/release-fixture.json`.

```powershell
pwsh -NoProfile -File .sdd/scripts/prepare-release-fixture.ps1
pwsh -NoProfile -File .sdd/scripts/test-release-fixture.ps1 -FixtureRoot <returned-path>
```

Snapshot has a local fixture commit only. Remote CI still must pass on the final PR revision. Recreate fixture after source changes affecting checks; historical reports do not prove a newer candidate.

## Rollback

For a normal release rollback, restore the previously reviewed canonical and generated revision together through a reviewed Git change. Preserve current `.specs/`, evidence history, independent edits, and runtime settings. Revalidate affected stages; rollback never makes stale evidence current.

For migration rollback to original Claude definitions, first preview:

```powershell
pwsh -NoProfile -File .sdd/scripts/rollback-adapters.ps1
```

Review restore/remove list before rerunning with `-Apply`. Script verifies baseline archive and ownership hashes, refuses independent edits, restores original adapter files, and retains shared core. This migration rollback differs from selecting a previous portable release. Regeneration enables adapters again.

## Publish

Review final diff, clean candidate results, native acceptance, and remaining waivers. Invoke Ship explicitly when ready. Push, PR, merge, and branch deletion retain existing confirmation boundaries. This preparation authorizes none of those actions automatically.
