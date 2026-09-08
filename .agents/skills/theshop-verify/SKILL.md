---
name: theshop-verify
description: "Deprecated user-facing smoke check: build, run, observe acceptance criteria without source edits. Backend-only features skipped; prefer $theshop-e2e."
---

<!-- Generated from .sdd/skills/theshop-verify/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/codex/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# $theshop-verify

**Feature requested:** `{arguments}`

Build solution, launch Web app, and verify each spec acceptance criterion against live feature. Never edit source; run, observe, report. For backend-only features, report skip.

## Scope

Legacy browser check between `$theshop-test` and `$theshop-review`. Prefer `$theshop-e2e`; preserve this workflow for remaining callers.

---

## Inputs

If `{arguments}` is empty, stop and ask:

> "Please provide a feature name. Usage: `$theshop-verify <feature-name>` — for example, `$theshop-verify add-to-cart`. The feature must have a spec at `.specs/{feature_name}/spec.md`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run in order. A failure halts the gate.

### Pre-flight 1 — Spec and plan must exist

- `.specs/{arguments}/spec.md` must exist — it holds the **Acceptance Criteria** that are this gate's pass/fail oracle. If missing, halt:

  > "I couldn't find a spec at `.specs/{arguments}/spec.md`. Verification checks the spec's acceptance criteria against the running app — create the spec first (`$theshop-spec {arguments}`)."

- `.specs/{arguments}/plan.md` should exist — it tells you whether the feature is user-facing and which routes/Figma nodes it introduces. If missing, warn but continue (you can still verify ACs by exploring the app).

### Pre-flight 2 — Is this feature user-facing? (the gate's applicability test)

This gate only applies to features a user can see and touch. Decide from the plan and the code:

- **User-facing** if the plan has a **Phase 4 — Web** with tasks, **or** a **Figma references** block, **or** the feature shipped `.razor` pages/components under `src/TheShop.Web/`.
- **Backend-only** if the plan touches only Domain / Application / Infrastructure (entities, handlers, repositories, schema) with no Web phase and no UI files.

If the feature is **backend-only**, do not launch the app. Emit the **N/A verdict** (template below) and stop:

> "`{arguments}` is backend-only — no Web-layer surface to drive. E2E verification doesn't apply; its unit/integration tests (`$theshop-test {arguments}`) are the appropriate gate. ⏭️ Skipped."

### Pre-flight 3 — Solution must build clean

```bash
dotnet build TheShop.slnx --nologo
```

If the build is red, halt — there's nothing to run:

> "The solution doesn't build, so there's no app to verify. Fix the build errors (or run `$theshop-test {arguments}` to see them in context), then re-invoke me."

---

## Step 1 — Assemble the smoke checklist from the spec

Use spec as behavior oracle. From `.specs/{arguments}/spec.md`, extract:

- **Section 6 — Acceptance Criteria** → the pass/fail checklist. Each AC is one row in your verdict.
- **Section 3 — Functional Behaviors** ("User does / User sees") → the concrete click-path for exercising each AC.
- From `.specs/{arguments}/plan.md`: the **route(s)** the feature adds (Section 6/7) and the **Figma node intent notes** (Phase 4) → where in the app to look and what it should resemble.

Present this checklist to the user before launching, so scope is clear.

---

## Step 2 — Launch the app

Skip this step when Tier 1 applies — the E2E fixtures own app launch and teardown.

Start the Web app in the background (it does not return on its own):

```bash
dotnet run --project src/TheShop.Web --launch-profile http
```

- The `http` profile serves at **http://localhost:5218** (see `src/TheShop.Web/Properties/launchSettings.json`).
- Poll readiness — `curl -s -o NUL -w "%{http_code}" http://localhost:5218` until it returns `200` (or time out at ~90s).
- **Watch the run output for startup exceptions** (unhandled exception, DI failure, missing config). A startup crash is an automatic 🔴 — capture the exception text as evidence.

If the app never becomes ready, halt with 🔴 and quote the last lines of the run log.

---

## Step 3 — Drive the feature and check each AC

Verify each acceptance criterion against the running app. Use the strongest tier available:

- **Tier 1 — Automated (preferred):** if `tests/TheShop.E2E.Tests` contains tests stamped `[Trait("Feature","{arguments}")]` (check with `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E&Feature={arguments}" --list-tests`), run the E2E environment script (`pwsh tests/TheShop.E2E.Tests/tools/start-e2e-env.ps1`), then `dotnet test tests/TheShop.E2E.Tests --filter "Category=E2E&Feature={arguments}"`. The E2E fixtures launch and tear down the app themselves — skip Step 2's manual launch when running this tier. Map each test's pass/fail to its AC by the `AC{n}_` prefix in the test name. Any **skipped** test means the environment didn't start — treat as a halt (Template C), never as a pass. ACs with no matching `AC{n}_` test fall through to Tier 2 for that AC only.
- **Tier 2 — Guided manual (fallback, for ACs with no Tier 1 journey):** this is a Blazor **WebAssembly** app, so the page renders client-side — a raw `curl` of a route returns the host shell, **not** the rendered component. That confirms the app *serves* but cannot confirm a component *rendered or behaves*. So:
  1. Confirm the app is serving (host page returns 200, no startup errors in the log).
  2. Hand the user the URL and a per-AC click-path (derived from Functional Behaviors), and ask them to confirm each AC **Pass / Fail** in their browser. Present them as a tight checklist; wait for their answers.
  3. Mark any AC the user did not explicitly confirm as **⚠️ Unconfirmed** (treated as not-passed for the verdict).

Report actual tier. Never label guided manual checks automated.

---

## Step 4 — Tear down

Always stop the background app when verification ends (success, failure, or halt). Leave no orphaned `dotnet` process bound to the port. Confirm the port is free in your report.

---

## Update the status tracker

Updating the feature's tracking artifact is not a source edit, so it's allowed here. After the verdict settles, update `.specs/{arguments}/status.md`: set the **Verify** row to State `Verified` (Template A, ✅ VERIFIED), `Pending` (Template A, 🔴 NOT VERIFIED — leave it open for a re-run), or `Skipped` (Template B, backend-only); Gate `✅ E2E pass (Tier {1|2})` or `🔴 {N} AC failed/unconfirmed` or `⏭️ backend-only`; Evidence one line of the AC tally and driver tier (e.g. `12 pass · 0 fail · Tier 2 guided manual`) — **always name the tier; a Tier 2 pass is user-confirmed, not automated, and the ledger must say so**; today's date. Refresh **Last updated**; point **Next step** at `$theshop-review {arguments}`. Leave the tracker untouched on Template C (halted before driving). Create `status.md` from the `theshop-spec` template first if it's missing.

## Outputs and completion evidence

Read `references/reports.md` before reporting. Use exact A/B/C template for verification, backend-only skip, or pre-drive halt. Preserve AC pass/fail/unconfirmed totals and actual driver tier. No surrounding prose.

## Rules (enforce strictly)

1. **Never edit source code in this command.** You build, run, observe, and report. If an AC fails, that's a finding for the user to act on — fixes happen elsewhere (`$theshop-implement`, `$theshop-review`).
2. **Always tear down the app.** No orphaned process, no port left bound — even when you halt.
3. **Don't fake a pass.** A guided-manual check the user didn't confirm is ⚠️ Unconfirmed, never ✅. A `curl` 200 on the WASM host shell is not AC evidence — it only proves the app serves.
4. **Backend-only features skip, they don't fail.** ⏭️ is the correct outcome there, not 🔴.
5. **The spec's acceptance criteria are the oracle.** Don't invent pass/fail criteria of your own — verify what the spec says "done" means.
