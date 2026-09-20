---
name: theshop-verify
description: Verify a feature's acceptance criteria against the running application without editing source.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Verify Feature

Canonical live-behavior gate after `$theshop-test`.

Build, run, observe, report. Never edit source or tests. Failed behavior returns to `$theshop-execute`.

## Input

Require one safe feature folder name.

Require `.specs/{feature}/spec.md`. Plan is optional but useful for routes, Web scope, and Figma intent.

The spec acceptance criteria are the oracle.

## Applicability

User-facing when any applies:

- plan Web phase has tasks
- plan has Figma references
- feature changed `.razor` files under `src/TheShop.Web/`

Backend-only: do not launch. Mark Verify `Skipped` with `⏭️ backend-only`; next `$theshop-ship {feature}` or optional `$theshop-document {feature}`.

## Pre-flight

```powershell
dotnet build TheShop.slnx --nologo
```

Red: halt. Do not verify a build that cannot run.

Extract:

- §6 Acceptance Criteria: checklist
- §3 Functional Behaviors: click paths
- plan routes/Figma notes: navigation and intent

Show compact checklist before driving.

## Driver

Use strongest available proof.

### Tier 1 — existing automation

If matching E2E journeys exist:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1
dotnet test tests/TheShop.E2E.Tests/TheShop.E2E.Tests.csproj --filter "Category=E2E&Feature={feature}" --logger "console;verbosity=normal" --nologo
```

Map `AC{n}_` tests to ACs. Skipped is not passed. Uncovered ACs fall to Tier 2.

Use `$theshop-e2e {feature}` when journeys must be created or repaired; this skill does not edit them.

### Tier 2 — guided manual

Launch:

```powershell
dotnet run --project src/TheShop.Web --launch-profile http
```

Wait up to 90 seconds for `http://localhost:5218`. Startup exception or no readiness: fail.

A raw HTTP 200 proves only that the WASM host serves. It does not prove rendered behavior.

Give the user the URL and one click path per uncovered AC. Require explicit Pass/Fail. No answer = `⚠️ Unconfirmed`.

## Teardown

Always stop the app process tree started here and confirm port 5218 is free. Tier 1 fixtures own their app lifecycle. Leave no orphan.

## Verdict

For every AC record:

- `✅ Pass`
- `❌ Fail`
- `⚠️ Unconfirmed`

Verdicts:

- `✅ VERIFIED`: build/start clean and every AC explicitly passed
- `🔴 NOT VERIFIED`: any failure, skip, startup error, or unconfirmed AC
- `⏭️ SKIPPED`: backend-only
- `⛔ HALTED`: missing spec or red build before driving

State the driver: Tier 1, Tier 2, or mixed. Never present user-confirmed proof as automated.

## Tracker

Update `.specs/{feature}/status.md` after a settled verdict:

- verified: Verify `Verified`; gate `✅ E2E pass (Tier {1|2|mixed})`
- not verified: Verify `Pending`; gate `🔴 {n} failed/unconfirmed`
- backend-only: Verify `Skipped`; gate `⏭️ backend-only`
- Evidence: AC tally + driver tier
- Date today; refresh `Last updated`
- Next: optional `$theshop-document {feature}`, otherwise `$theshop-ship {feature}`

Do not update tracker when halted before driving.

## Output

Return only a compact verification report:

- surface/routes
- build and launch
- driver tier
- AC result table
- teardown
- verdict
- exact next command
