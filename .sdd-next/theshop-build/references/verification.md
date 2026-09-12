# Verification and completion

Read while assigning proof during Understand. Reuse during implementation and final checks. Each acceptance criterion needs observed proof suited to its behavior; more than one proof type may apply.

## Choose proof

| Behavior | Proof |
|---|---|
| Domain rules, validation, handler outcomes | Unit tests in matching project |
| Component rendering, events, local UI state | bUnit component tests |
| Repository queries, constraints, migrations, RLS | Integration tests against local database/Testcontainers |
| User journey across running UI and services | Browser E2E in `tests/TheShop.E2E.Tests/` |
| Visual layout or externally controlled behavior | Browser inspection or explicitly identified human proof |

Domain uses xUnit/FluentAssertions; Application uses established NSubstitute patterns; Web uses bUnit. Match existing infrastructure and E2E runner configuration. Name tests `{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`. Assert observable requirements and meaningful failure/boundary cases. Required new members each receive matching tests.

User-facing changes need browser proof of affected journey. Maintain repeatable E2E for changed interactions where feasible. Pure visual changes may use focused browser inspection with saved evidence. Backend-only work records browser E2E as not applicable, with reason. Mocked component tests never count as E2E; mocked network journeys cannot prove real persistence or RLS.

## Database test lifecycle

Unit/component tests using mocks need no database reset. For integration/E2E suites using real data, establish one clean baseline per suite on a dedicated disposable local database. Fresh Testcontainers fixtures already provide this baseline; do not reset them redundantly. Reuse existing fixture/setup hooks rather than adding a separate test orchestrator.

Before resetting an existing local Supabase test stack, verify endpoint/project, disposable status, and absence of other active users or test suites. `supabase db reset --local` destroys local data, replays migrations, and loads configured seeds. Use only verified disposable test data or obtain explicit authorization for data loss. Never reset a shared developer database or remote target implicitly.

Keep seeds small, deterministic, and synthetic. Each test owns identifiable rows, test-created users, and uploaded objects; clean them through teardown even after failure. Use existing supported cleanup APIs and fixture lifetimes. Never delete shared seed identities or another test's data. If cleanup fails, report contaminated state and restore a known baseline before dependent runs; never suppress failure as clean success.

Do not reset before each test or retry. Tests must not depend on execution order. Parallel tests need isolated owned data or separate fixtures; serialize suites sharing a resettable database. For migration tests, follow [upgrade and replay sequence](migrations.md#2-apply-and-test-locally) instead of resetting directly to latest schema before upgrade proof.

Record target, baseline/seed method, cleanup outcome, and any contamination in feature verification. A database test can need this evidence even when feature adds no migration.

## Docker resources

Local Supabase runs PostgreSQL and supporting services in Docker. Keep test data/uploads bounded through lifecycle above. Database reset does not clean Docker images or build cache.

When storage pressure appears, inspect `docker system df -v` before choosing targeted cleanup. Do not automatically prune images, volumes, or unrelated project data. Stop test stacks started by this run when work finishes, after confirming no other task needs them. Leave pre-existing/shared stacks running unless user requests shutdown. Use normal `supabase stop` to retain local database data; never add `--no-backup` as routine cleanup.

## Visible browser runs

Local pilot E2E runs use a headed browser on the user's visible desktop. Headless runs require explicit user selection; CI may use headless execution. A headed setting alone does not prove the user could see the window: an isolated execution session may hide it.

Inspect actual browser launch configuration. Current `PlaywrightFixture` reads `E2EEnvironment.Headless`; `E2E_HEADED` is only mentioned in comments and is not an active switch. Confirm `Headless = false` for local runs. Rebuild affected E2E binaries before using `--no-build` after configuration or source changes.

Use an interactive execution context and obtain required tool permission to show the browser. If desktop visibility cannot be established, report that limitation before running; do not silently fall back to headless or claim visible-browser proof. No need to rerun an already completed feature merely because this policy changed unless user requests it.

Record browser mode and observed window visibility with E2E results. A visible browser running automated journeys satisfies this preference; a separate test-runner dashboard is not required unless requested.

## Execute

1. Establish relevant baseline before behavioral edits where practical. Distinguish pre-existing failures from regressions.
2. After edits, format only feature-owned C#/Razor files using `dotnet format TheShop.slnx --no-restore --include <paths>`. Resolve paths from actual changed files; preserve unrelated dirty work.
3. Build final sources: `dotnet build TheShop.slnx --nologo`. Prepare database suites through lifecycle above, then run focused unit/component/integration tests via actual project paths and existing runner options. A zero-test run proves nothing; record discovered, passed, failed, and skipped counts. Broaden tests when shared behavior changed.
4. Run isolated design checker against changed production C#/Razor files: `pwsh -NoProfile -File .sdd-next/scripts/check-design-rules.ps1 -Path <paths>`. In PowerShell pass multiple paths as an array within the same invocation. No relevant files means not applicable; never pass nonexistent paths.
5. Start required local services using existing project setup and establish database suite baseline when applicable. Run affected browser journeys; preserve actual traces/screenshots and command output. Complete owned-data cleanup and service lifecycle above. Do not invent credentials, runner flags, or service success. If setup fails, retain blocked acceptance and explain missing prerequisite.
6. Review changed code against relevant constitution and references. Mechanical checker covers only a subset. Verify English/French key parity, real translations, format placeholders, public contracts, permissions, and required member tests. Apply independent review when security reference requires it.
7. Fix in-scope findings, then repeat checks affected by fixes. Confirm observed behavior against agreed acceptance, including explicit human proof if required.

Run graph refresh once after code work when root project policy requires it; graph is a derived index, not acceptance evidence. Do not edit legacy SDD records to record pilot verification.

## Evidence and stopping

Keep results in `feature.md`: exact command, exit code, counts, acceptance mapping, time/revision, browser mode/window visibility for E2E, and relative log/trace path. Save full outputs under feature `evidence/` only when produced. Do not expose secrets or personal data. No separate manifest or success receipt required.

Verification table records what actually ran and how to reproduce it. Exit code 0 means command succeeded; test counts and acceptance evidence establish what it checked. A successful build does not prove feature behavior. For manual inspection, name the inspected behavior and evidence instead of inventing a command.

`Pass` means observed success. `Fail`, `Blocked`, `Pending`, and `Not applicable` remain distinct. Required skipped tests or outstanding human/independent review block `Done`. Record reasons for every non-pass result. Explicit user-approved scope changes update acceptance and its decision record; never relabel missing proof as passed.

Before `Done`, all current acceptance criteria and applicable checks pass; no unresolved material decision or unreviewed sensitive change remains. Record revision and working-tree context; tests include uncommitted code. On resume, inspect changed code/environment and repeat stale or uncertain proof. Never infer freshness solely from Git HEAD or prior `Done` label.

For database changes, follow [migration lifecycle](migrations.md). Local proof and remote deployment status remain separate. Remote verification gates `Done` only when deployment is part of agreed scope; otherwise report pending remote work explicitly. Required local upgrade/replay and permission tests cannot be replaced by remote SQL success.

Stop after these conditions. No repeated full-suite runs, extra refactoring, speculative tests, or documentation stages without new changes or evidence requiring them.
