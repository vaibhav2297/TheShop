---
description: Build and run the app, then smoke-check a feature's acceptance criteria against the running build. A user-facing E2E gate — skipped for backend-only features. Read-only with respect to source; it runs and observes, never edits code.
argument-hint: <feature-name>
---

# /theshop.verify

**Feature requested:** `$ARGUMENTS`

You are running the **end-to-end verification gate** for **The Shop**. Your job is to prove the feature actually works in the *running application* — not just that unit/component tests pass. You build the solution, launch the Web app, reach the feature, and confirm each **acceptance criterion** from the spec against the live build. You **never edit source code** in this command — you run, observe, and report. If the feature is backend-only, you say so and skip.

This gate sits between `/theshop.test` (steps run against compiled code) and `/theshop.review` (quality + security). Tests prove the units are correct; this proves the feature is *real* in a browser.

---

## Input validation

If `$ARGUMENTS` is empty, stop and ask:

> "Please provide a feature name. Usage: `/theshop.verify <feature-name>` — for example, `/theshop.verify add-to-cart`. The feature must have a spec at `.claude/specs/{feature_name}.md`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run in order. A failure halts the gate.

### Pre-flight 1 — Spec and plan must exist

- `.claude/specs/$ARGUMENTS.md` must exist — it holds the **Acceptance Criteria** that are this gate's pass/fail oracle. If missing, halt:

  > "I couldn't find a spec at `.claude/specs/$ARGUMENTS.md`. Verification checks the spec's acceptance criteria against the running app — create the spec first (`/theshop.spec $ARGUMENTS`)."

- `.claude/plans/$ARGUMENTS.md` should exist — it tells you whether the feature is user-facing and which routes/Figma nodes it introduces. If missing, warn but continue (you can still verify ACs by exploring the app).

### Pre-flight 2 — Is this feature user-facing? (the gate's applicability test)

This gate only applies to features a user can see and touch. Decide from the plan and the code:

- **User-facing** if the plan has a **Phase 4 — Web** with tasks, **or** a **Figma references** block, **or** the feature shipped `.razor` pages/components under `src/TheShop.Web/`.
- **Backend-only** if the plan touches only Domain / Application / Infrastructure (entities, handlers, repositories, schema) with no Web phase and no UI files.

If the feature is **backend-only**, do not launch the app. Emit the **N/A verdict** (template below) and stop:

> "`$ARGUMENTS` is backend-only — no Web-layer surface to drive. E2E verification doesn't apply; its unit/integration tests (`/theshop.test $ARGUMENTS`) are the appropriate gate. ⏭️ Skipped."

### Pre-flight 3 — Solution must build clean

```bash
dotnet build TheShop.slnx --nologo
```

If the build is red, halt — there's nothing to run:

> "The solution doesn't build, so there's no app to verify. Fix the build errors (or run `/theshop.test $ARGUMENTS` to see them in context), then re-invoke me."

---

## Step 1 — Assemble the smoke checklist from the spec

You verify against the spec, because its acceptance criteria are written to be *observable from the outside* — exactly what an E2E check needs. From `.claude/specs/$ARGUMENTS.md` extract:

- **Section 6 — Acceptance Criteria** → the pass/fail checklist. Each AC is one row in your verdict.
- **Section 3 — Functional Behaviors** ("User does / User sees") → the concrete click-path for exercising each AC.
- From `.claude/plans/$ARGUMENTS.md`: the **route(s)** the feature adds (Section 6/7) and the **Figma node intent notes** (Phase 4) → where in the app to look and what it should resemble.

Present this checklist to the user before launching, so scope is clear.

---

## Step 2 — Launch the app

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

- **Tier 1 — Automated (preferred, if a browser-automation tool is connected):** drive the feature's route, perform the Functional-Behavior actions, and assert each AC's observable outcome. Record pass/fail with concrete evidence (what you saw).
- **Tier 2 — Guided manual (fallback — current default, since no browser-automation MCP is connected):** this is a Blazor **WebAssembly** app, so the page renders client-side — a raw `curl` of a route returns the host shell, **not** the rendered component. That confirms the app *serves* but cannot confirm a component *rendered or behaves*. So:
  1. Confirm the app is serving (host page returns 200, no startup errors in the log).
  2. Hand the user the URL and a per-AC click-path (derived from Functional Behaviors), and ask them to confirm each AC **Pass / Fail** in their browser. Present them as a tight checklist; wait for their answers.
  3. Mark any AC the user did not explicitly confirm as **⚠️ Unconfirmed** (treated as not-passed for the verdict).

Be explicit in your report about which tier you used — don't present a guided-manual check as if it were automated.

---

## Step 4 — Tear down

Always stop the background app when verification ends (success, failure, or halt). Leave no orphaned `dotnet` process bound to the port. Confirm the port is free in your report.

---

## Verdict

Emit exactly one template. No extra prose.

### Template A — Verification ran

```markdown
# E2E Verification — $ARGUMENTS

## Scope
- Spec: `.claude/specs/$ARGUMENTS.md` ✅
- Surface: user-facing ({route(s) checked})
- Driver tier: {Tier 1 — automated / Tier 2 — guided manual}

## App launch
- Build: ✅ clean
- Launch: {✅ ready at http://localhost:5218 / 🔴 failed to start}
- Startup errors: {None / quote the exception}

## Acceptance criteria
| AC | Result | Evidence / note |
|---|---|---|
| AC-1 | {✅ Pass / ❌ Fail / ⚠️ Unconfirmed} | {what was observed, or why unconfirmed} |
| AC-2 | {✅ Pass / ❌ Fail / ⚠️ Unconfirmed} | … |

**AC status:** {N} pass · {N} fail · {N} unconfirmed

## Teardown
- App stopped; port 5218 free.

## Verdict
{One of:}
- **✅ VERIFIED — feature works in the running app**  *(use only when the app launched with no startup errors AND every AC is ✅ Pass)*
- **🔴 NOT VERIFIED**  *(use for any startup error, any ❌ Fail AC, or any ⚠️ Unconfirmed AC)*

{One sentence justifying it. For 🔴, name the failing/unconfirmed AC(s) or the startup error.}
```

### Template B — Not applicable (backend-only)

```markdown
# E2E Verification — $ARGUMENTS

**⏭️ Skipped — backend-only feature.**

`$ARGUMENTS` has no Web-layer surface (no Phase 4 / Figma references / `.razor` files), so there's nothing to drive in a browser. Its unit and integration tests via `/theshop.test $ARGUMENTS` are the appropriate gate.
```

### Template C — Halted before driving

```markdown
# E2E Verification — $ARGUMENTS

**🔴 Halted — could not verify.**

**Reason:** {missing spec / red build / app failed to start — quote the evidence}

**Next step:** {what the user must fix, then re-run `/theshop.verify $ARGUMENTS`.}
```

---

## Rules (enforce strictly)

1. **Never edit source code in this command.** You build, run, observe, and report. If an AC fails, that's a finding for the user to act on — fixes happen elsewhere (`/theshop.implement`, `/theshop.review`).
2. **Always tear down the app.** No orphaned process, no port left bound — even when you halt.
3. **Don't fake a pass.** A guided-manual check the user didn't confirm is ⚠️ Unconfirmed, never ✅. A `curl` 200 on the WASM host shell is not AC evidence — it only proves the app serves.
4. **Backend-only features skip, they don't fail.** ⏭️ is the correct outcome there, not 🔴.
5. **The spec's acceptance criteria are the oracle.** Don't invent pass/fail criteria of your own — verify what the spec says "done" means.
