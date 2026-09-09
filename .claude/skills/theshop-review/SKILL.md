---
name: theshop-review
description: "Review feature diff through independent parallel security/quality agents; unify findings and apply fixes only after approval."
argument-hint: "<feature-name>"
---

<!-- Generated from .sdd/skills/theshop-review/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# /theshop-review

**Feature requested:** `$ARGUMENTS`

Orchestrate parallel security and quality reviewers. Review is read-only until user explicitly approves action plan in chat. Only then implement approved fixes; never edit files before approval.

---

## Inputs

If `$ARGUMENTS` is empty (the user invoked `/theshop-review` with no feature name), stop and ask:

> "Please provide a feature name. Usage: `/theshop-review <feature-name>` — for example, `/theshop-review add-to-cart`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run checks. If fails halts the whole flow.

### Pre-flight — Spec must exist (halt)

Check `.specs/$ARGUMENTS/spec.md` exists. If not, halt:

> "I couldn't find a spec at `.specs/$ARGUMENTS/spec.md`. Review verifies the diff against a feature's ratified record — create the spec first (`/theshop-spec $ARGUMENTS`), or pass the right feature name."

Run spec-existence gate first (Rule 4).

### Pre-flight — Diff must be non-empty

Collect the current diff:

```bash
git diff                # unstaged
git diff --staged       # staged
```

If empty, halt immediately with the exact message:

> "No changes detected. Implement the feature before running code review."

Do not invoke the reviewers on an empty diff.

If a diff exists, briefly note what files are changing (output of `git diff --name-only` and `git diff --staged --name-only`) so the user can confirm scope before the reviewers start. One line each, no commentary.

### Pre-flight — French localization completeness (gate)

Every added English user-facing string requires real French translation. `[TODO]` placeholders block shipping.

Run the check (do **not** halt — record the result and carry it into the report):

1. From the diff, collect every resource key **added or modified** in `src/TheShop.Web/Resources/Strings.resx` (the `name="..."` of each changed `<data>` entry).
2. For each such key, inspect the same key in `src/TheShop.Web/Resources/Strings.fr.resx`. A key **fails** the gate if its French entry is **missing**, **empty**, or still contains the literal placeholder `[TODO]`.

   ```bash
   git diff -- src/TheShop.Web/Resources/Strings.fr.resx
   ```
   (Also scan the current `Strings.fr.resx` for `[TODO]` among the feature's keys, in case the placeholder predates this diff.)
3. **Result handling:**
   - **No untranslated keys** → record "Localization: ✅ all feature keys translated" and proceed normally.
   - **One or more untranslated keys** → this is a **blocking** finding. Record each failing key. It becomes a mandatory item in the Combined Action Plan under **Must fix before committing**, and it forces the overall verdict to 🔴 **CHANGES REQUESTED** regardless of what the two reviewers find. Untranslated user-facing strings are a ship-blocker, not a suggestion.

This gate is independent of the two reviewers — it runs whether or not they surface anything.

### Pre-flight — Deterministic design-rule lint (gate)

The mechanically-checkable constitution rules are enforced by a script — the same one the PostToolUse hook runs on every edit. Run it over the changed source files (do **not** halt — record the result and carry it into the report):

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-design-rules.ps1 -Path {each changed file under src/ from the diff}
```

- **Exit 0** → record "Design-rule lint: ✅ clean" and proceed.
- **Exit 1** → every reported violation is a **blocking** finding. Each cites its constitution rule number (e.g. `Rule 16`). They go into the Combined Action Plan under **Must fix before committing** and force the overall verdict to 🔴 **CHANGES REQUESTED**, same as the localization gate. Do not re-litigate them — the script is the authority for these rules; the only escape hatch is a justified `design-rules: ignore` line comment, which a human must approve.

Carry mechanical findings into report before judgment-based review.

### Pre-flight — Test-manifest drift gate

If `.specs/$ARGUMENTS/test-manifest.json` exists, run (do **not** halt — record the result and carry it into the report):

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 manifest -Feature $ARGUMENTS
```

- **Exit 0** → record "Manifest drift: ✅ test record still matches reality" and proceed.
- **Exit 1** → the feature's ratified test record has drifted from the code (renamed keys, moved files, stripped traits — the kind of rot a manual commit leaves behind). Every violation is a **blocking** finding under **Must fix before committing** and forces 🔴 **CHANGES REQUESTED**, same as the localization and lint gates. Stale test records are how green verdicts become lies.
- **No manifest** → record "Manifest drift: ⏭️ skipped — no test manifest yet (run `/theshop-test $ARGUMENTS`)". Not blocking.

### Pre-flight — New code has tests (Rule 29 gate)

From the diff's file list, identify every **new** production file matching these shapes: `*Handler.cs`, `*Repository.cs`, `*Validator.cs`, Domain entities/value objects under `src/TheShop.Domain/`. For each, check the diff also touches at least one file under `tests/` that plausibly covers it (matching name or feature folder). Do **not** halt — record the result:

- All covered → record "Rule 29: ✅ new units have tests".
- Any new unit with no corresponding test change → a **blocking** finding under **Must fix before committing** citing Rule 29, naming each untested unit. This is the only place in the pipeline Rule 29 can be enforced for code written outside `/theshop-implement`.

---

## Step 1 — Parallel review

Launch **both** reviewer delegation calls in **one response**. Never wait for one before launching the other.

The two calls:

1. `role: shop-code-quality-review`
2. `role: shop-code-security-reviewer`

Both get the same prompt template:

> "Review the recently changed code for the feature `$ARGUMENTS`. Use the current uncommitted + staged diff (`git diff` and `git diff --staged`) as your scope — no need to ask the user about scope, it's already been confirmed. Follow your standard protocol and deliver your full structured report."

Wait for **both** to complete before doing anything else.

---

## Step 2 — Verify both succeeded

Inspect the two returned reports.

- If **either** agent returned an empty result, errored out, halted on its own pre-flight (e.g., couldn't read the `theshop-constitution` skill files), or produced a report missing any of its required sections (`🎓 / 💡 / 🌱 / ✅`), **stop**. Do not present a unified report.

  Tell the user:

  > "One of the reviewers couldn't complete: `{agent-name}` returned `{short description of what happened}`. I'm not going to present a partial review — please address the issue and re-run me."

- If **both** agents returned complete reports, proceed to Step 3.

---

## Step 3 — De-duplicate and merge

Walk both reports together. For each finding:

- **Same file, same line range, same underlying issue** → merge into one combined finding. Keep both reviewers' phrasings, prefixed by which lens it came from. Example merge:

  > **`Pages/Admin/AdminOrders.razor:48`** — Logging the customer's email address inside a long inline handler.
  > - *Security:* PII in logs — emails shouldn't be written to `_logger`; log the user ID instead.
  > - *Quality:* the handler is doing three distinct things and would read better split into a dedicated method.

- **Same file, different lines or different issues** → keep separate.
- **Different files** → keep separate.

Preserve both perspectives when concerns overlap.

---

## Step 4 — Unified report

Read `references/unified-report.md`; use its exact structure without surrounding prose. Apply verdict and approval rules below.

### Verdict selection rules (be strict)

Pick the verdict using only these rules — no judgment calls:

| Condition | Verdict |
|---|---|
| Any untranslated French feature key (localization pre-flight failed), OR any design-rule lint violation (lint pre-flight failed), OR any manifest-drift violation (drift pre-flight failed), OR any untested new unit (Rule 29 pre-flight failed), OR any 🚨 Critical security finding, OR any ⚠️ Important security finding, OR any 💡 Quality "Worth improving" finding | 🔴 **CHANGES REQUESTED** |
| No items above, BUT at least one unmarked security finding, OR at least one 🌱 Quality "Polish" item | 🟡 **APPROVED WITH SUGGESTIONS** |
| Nothing in any "must fix" or "worth addressing" bucket — only ✅ Doing well | ✅ **APPROVED** |

Critically: even one critical security finding — or a single untranslated French feature key — overrides everything else and forces 🔴, regardless of how many ✅ items the reviewers found.

### Update the status tracker

Once the verdict is set, update the feature's tracking artifact `.specs/$ARGUMENTS/status.md` (this is the one `.specs/*` write this command permits): set the **Review** row to State `Approved` (✅ APPROVED or 🟡 APPROVED WITH SUGGESTIONS) or `Changes requested` (🔴 CHANGES REQUESTED); Gate a one-line roll-up of the four deterministic pre-flights (e.g. `✅ FR/lint/drift/R29 pass` or `🔴 lint ×2, drift ×1`); Evidence one line (e.g. `2 security + 3 quality findings · verdict 🟡`); today's date. Refresh **Last updated**, and point **Next step** at `/theshop-document` (approved) or back at the action plan (changes requested). Create `status.md` from the `theshop-spec` template first if it's missing.

---

## Step 5 — Ask for approval

After presenting the unified report, ask exactly:

> "Do you want me to implement the action plan now?"

Then **stop**. Do not touch any file. Wait for the user's reply in chat.

### Interpreting the user's reply

- Explicit yes ("yes", "go ahead", "implement", "do it") → proceed to Step 6.
- Explicit no ("no", "not now", "I'll handle it") → acknowledge and end. Do not modify files.
- Partial yes ("just the security ones", "everything except #4") → ask one short clarifying question to confirm exactly which items, then proceed to Step 6 with that scoped list.
- Anything ambiguous → ask one clarifying question. Do not assume yes.

If the verdict was ✅ APPROVED, there's nothing to implement — skip Step 5 entirely and just say "Nothing to fix. Looks good."

---

## Step 6 — Implementation (only after explicit approval)

This is the only phase in which you may edit files. Apply the action plan items the user approved, in the order listed. Some practical rules:

- **Make one focused edit per finding.** Don't batch unrelated changes into a single edit.
- **Items that require a design call** (e.g., "this DTO is over-sharing — should `IsAdmin` be exposed to the cart page?") are **not** straight-line fixes. For each such item, stop and ask the user *before* editing. Don't make architecture decisions on their behalf.
- **Items outside `tests/` and `src/`** (e.g., changes to `.github/`, `appsettings.json` in `wwwroot/`, secrets) are higher-risk. For these, summarize the proposed change and ask the user to confirm before writing.
- **If a finding turns out to need spec changes** (the spec disagrees with the code, and the code is actually right), don't edit the spec automatically. Flag it and tell the user to re-run `/theshop-spec $ARGUMENTS` (or `/theshop-clarify`) or update the spec by hand.

When implementation is finished, refresh the knowledge graph so the applied fixes are reflected in it (skip silently if `graphify` or `graphify-out/graph.json` is unavailable — it writes only under `graphify-out/`, never source):

```bash
graphify update .
```

Then produce a short summary:

```markdown
## Implementation summary

**Applied:**
- ✅ Action #1: `{file:line}` — {what was changed in one line}
- ✅ Action #2: `{file:line}` — {one line}

**Skipped (needs your input):**
- ⏭️ Action #N: `{file:line}` — {why I stopped: design decision, ambiguous scope, etc.}

**Next step:** Re-run `/theshop-review $ARGUMENTS` to verify the fixes, or `/theshop-test $ARGUMENTS` to make sure nothing broke.
```

---

## Rules (enforce strictly)

1. **Do not apply fixes before explicit user approval in chat.** Review ledger and evidence records required by this workflow are permitted before fix approval; they record findings and grant no edit authorization. Production/test/configuration fixes remain approval-gated, including obvious one-line changes.
2. **Do not start one reviewer before the other.** Both delegation calls go out in the same response. Sequential invocation is a bug.
3. **Do not skip the pre-flight checks.** No spec → halt. No diff → halt. Both pre-flights run before any reviewer is invoked.
4. **Do not proceed if the spec file doesn't exist.** Report and stop. The spec gate is intentional.
5. **Do not present a partial review as complete.** If either reviewer fails, no unified report is produced — just an error report and stop.
6. **Do not edit any file under `.claude/skills/theshop-constitution/` or `.specs/*` in the implementation phase.** The skill's rules, references, examples, and checklists are governing documents, and a feature's `.specs/{feature}/` artifacts (spec, plan, manifest) are its ratified record — changes to them belong in a separate, deliberate flow, not in a quick fix loop. (Updating `.specs/$ARGUMENTS/status.md` to record the review outcome is the one allowed exception — see below.)
