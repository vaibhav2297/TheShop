---
name: theshop-document
description: "Add XML documentation to current C# diff through shop-code-documenter. Explicit standalone step; preserves behavior."
---

# {{command:theshop-document}}

Invoke `shop-code-documenter` for current diff. Add XML documentation regardless of how that code was produced.

## Inputs and scope

Current diff; optional feature name in `{arguments}` controls ledger update. Manual step after `{{command:theshop-test}}`, `{{command:theshop-verify}}`, and `{{command:theshop-review}}`. `{{command:theshop-implement}}` never runs documenter. Document settled code.

---

## Procedure

### 1. Pre-flight — diff must be non-empty

Run:

```bash
git diff --name-only
git diff --staged --name-only
```

If both are empty, halt:

> "Working tree is clean and no staged changes. I document recently changed code — make changes first (or specify a commit range) and re-invoke me."

If a diff exists, briefly list the changed files (output of the two commands above) so the user can confirm scope before the documenter starts.

### 1.5 Snapshot the pre-documentation state (baseline for the exit gate)

Before invoking the documenter, save the current state of every changed file aside:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 snapshot -Snapshot "$env:TEMP/theshop-doc-snapshot"
```

Snapshot isolates documenter changes from existing diff.

### 2. Invoke `shop-code-documenter`

Call the sub-agent via the delegation capability, `role: shop-code-documenter`. Prompt:

> "Add XML doc comments to the current diff (`git diff` + `git diff --staged`). Follow your standard protocol per `references/rules/documentation.md` and end with the structured summary."

Wait for it to complete.

### 3. Run the doc-only gate (exit gate — mandatory)

The documenter's defining promise is "no behavioral change." Verify it mechanically:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 doc-only -Snapshot "$env:TEMP/theshop-doc-snapshot"
```

Compare against Step 1.5 snapshot. Only XML doc-comment (`///`) lines may change. Code edits, formatting, added/deleted files, and non-`.cs` changes fail.

- **Exit 0** → proceed to Step 4.
- **Exit 1** → surface the violations **prominently above the documenter's report** and do not record the step as done. Do not fix or revert anything yourself — show the user exactly which lines changed beyond doc comments and let them decide (revert the stray lines, or keep them as a deliberate change outside this command's scope).

### 4. Relay the documenter's report

Return documenter's structured summary verbatim; no added commentary.

If the documenter reported a build failure or halted on a question, surface that prominently above the summary:

> "⚠️ Documenter halted — see report below for the reason."

### 5. Update the status tracker (only if a feature name was given)

If `{arguments}` named a feature **and** the documenter completed successfully **and** the doc-only gate passed, update `.specs/{arguments}/status.md`: set the **Document** row to State `Done`, Gate `✅ doc-only gate pass`, Evidence one line (e.g. `{N} files documented · build ✅`), today's date; refresh **Last updated**, and set **Next step** to `— (pipeline complete)`. Create `status.md` from the template in the `theshop-spec` skill first if it's missing. If no feature name was given (the common standalone case), skip this step — the command stays diff-scoped and touches no tracker.

---

## Hard rules

1. **Do not edit files yourself.** Delegate documentation; preserve Step 5's conditional ledger update.
2. **Do not invoke any other agent.** Quality, security, tests, implementers — all out of scope. If the user wants those, they'll run the dedicated commands.
3. **Do not loop the documenter.** One invocation per command run. If it fails, surface the failure; don't retry.
