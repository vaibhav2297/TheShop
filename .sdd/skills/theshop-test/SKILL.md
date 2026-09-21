---
name: theshop-test
description: Write, build, run, and report feature-scoped unit and component tests in one session.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Test Feature

Own the full non-browser test loop:

`read → map ACs → write → manifest gate → build → run → reconcile → report`

No sub-agents. Browser journeys belong to `$theshop-e2e`. Production fixes belong to `$theshop-execute`.

## Input and authority

Require one safe feature folder name: no separators or `..`.

Require `.specs/{feature}/spec.md`. Read plan when present; spec wins behavioral conflicts.

Load `$theshop-constitution` and its testing guidance. Inspect existing tests before writing.

May edit only:

- feature-relevant files under `tests/`, excluding shared E2E harness
- `.specs/{feature}/test-manifest.json`
- `.specs/{feature}/test-report.md`
- `.specs/{feature}/status.md`

Never edit `src/`, project/package files, dependencies, spec, plan, migrations, or shared harness. Missing production behavior is a finding.

## Coverage

Map every acceptance criterion to tests or an explicit browser/manual boundary.

Read [visual fidelity — Test / E2E](../theshop-plan/references/visual-fidelity.md) for UI work.
Visual ACs: `e2e`. Component/unit assertions never prove appearance.
Non-browser tests pass: route UI to `$theshop-e2e {feature}` for aligned baseline
and regression capture before Verify. Test Passing alone cannot prove fidelity.

Cover where applicable:

- happy path
- validation/failure
- edge and boundary behavior
- authorization/tenant isolation
- persistence or infrastructure contracts

Use existing project conventions. Test names describe behavior. Stamp each feature test method:

```csharp
[Trait("Feature", "{feature}")]
```

Keep tests deterministic. No real network, clock, or shared mutable state unless the existing integration harness owns it.

## Layer expectations

- Domain: invariants, transitions, value equality, invalid construction.
- Application: handler results, validation, mapping, authorization, dependency calls.
- Infrastructure: SQL/RPC/repository shape, parameterization, RLS-sensitive contracts; use existing integration patterns.
- Web/component: render states, events, validation, authorization, localized output.
- Do not duplicate pure logic through slow component tests.

## Manifest

Write `.specs/{feature}/test-manifest.json` with:

- feature and trait
- test projects
- test files/FQNs
- each spec AC mapped to one or more tests, or marked `e2e`/`manual` with reason
- expected discovered count

Then run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature {feature}
```

One correction pass. Still red: halt.

## Build

Build only manifested test projects first.

- Test-code failure: one focused repair, then rebuild.
- Production compile failure or missing production contract: halt; route to `$theshop-execute {feature}`.
- Never change production to make a test pass.

## Run

Run only manifested projects:

```powershell
dotnet test {project} --filter "Feature={feature}" --logger "console;verbosity=normal" --nologo --no-build
```

Never run the full suite unless explicitly requested.

Reconcile expected vs discovered tests. One mechanical repair for trait/name/manifest drift. Re-run affected project. A skipped test is not a pass.

Classify failures:

- test defect: repair once
- product defect: report; do not weaken assertion
- environment defect: report exact dependency/state
- unclear: fail closed as product/environment finding

## Report and tracker

Overwrite `.specs/{feature}/test-report.md` with:

- date and commit
- projects and filters
- expected/discovered/passed/failed/skipped
- AC → test evidence
- failures with exact FQN and output
- verdict: `✅ PASSING`, `🔴 FAILING`, or `⛔ HALTED`

Update `status.md`:

- success: Test `Passing`; gate `✅ manifest + filtered tests pass`
- failure/halt: Test `Pending`; gate names failure
- evidence links `test-report.md`
- date today; refresh `Last updated`
- success next: UI `$theshop-e2e {feature}`; backend `$theshop-verify {feature}`
- production gap next: `$theshop-execute {feature}`

Final response must match the persisted report. Never claim passing with failures, skips, count drift, or uncovered non-browser ACs.
