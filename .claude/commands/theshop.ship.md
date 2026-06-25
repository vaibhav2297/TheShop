---
description: Ship a finished feature — commit the work, push the feature branch, open a PR against dev, and (with explicit confirmation at each step) merge it, delete the branch, and return to dev. The git "post" bookend of the SDD pipeline; run it after /theshop.review.
argument-hint: <feature-name>
---

# /theshop.ship

**Feature requested:** `$ARGUMENTS`

You are the **post** git bookend for the SDD pipeline on **The Shop**. Your job is to take a finished feature branch and land it on `dev`: commit → push → open PR → (confirm) merge → (confirm) delete branch → switch back to `dev`. You run **after** `/theshop.review` has approved the diff.

Every step that touches the remote or is hard to reverse — push, PR creation, merge, branch deletion — is **gated behind an explicit user confirmation**. You never push, merge, or delete on assumption. Use the `gh` CLI for all GitHub operations.

The `{feature-name}` you take here is the **same slug** used for `.specs/{feature-name}/` and the `feature/{feature-name}` branch created by `/theshop.start`.

---

## Input validation

If `$ARGUMENTS` is empty, stop and ask:

> "Which feature are you shipping? Give me the feature name (e.g., `add-to-cart`) — the same one used for `.specs/{name}/` and the `feature/{name}` branch."

Wait for the reply. Do nothing else.

Normalize the name the same way `/theshop.spec` and `/theshop.start` do (lowercase, hyphen-separated). The branch is **`feature/{slug}`**.

---

## Pre-flight checks

Run in order.

### Pre-flight 1 — Must be on a feature branch (halt)

```bash
git rev-parse --abbrev-ref HEAD
```

- If the current branch is `dev` or `master`, halt:

  > "You're on `{branch}`. I won't open a PR from a base branch. Switch to the feature branch first (`git checkout feature/{slug}`) and re-run me."

- If the current branch is a `feature/*` branch but **not** `feature/{slug}`, warn and confirm:

  > "You're on `{current branch}` but asked to ship `{slug}`. Should I ship the branch you're on, or did you mean a different feature name?"

### Pre-flight 2 — Ledger-readiness gate (warn, waivable — do not halt)

Don't eyeball the tracker — let the script scan the whole ledger deterministically. If `.specs/{slug}/status.md` exists, run:

```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 ship-ready -Feature {slug}
```

The `ship-ready` mode walks all seven rows and flags every stage that isn't in a ship-ready terminal state (Spec `Confirmed`, Plan `Resolved`, Implement `Done`, Test `Passing`, Verify `Verified`/`Skipped`, Review `Approved`, Document `Done`), plus any recorded 🔴 gate failure or carried `⚠️ waived` from an earlier step.

- **Exit 0** → the whole pipeline is green. Record "Ledger: ✅ all stages ship-ready" and proceed.
- **Exit 1** → there are open items. **Do not silently ship over them.** Quote the script's violation lines verbatim and warn:

  > "Before I commit and ship `{slug}`, its SDD ledger still has open items:
  >
  > {paste the script's violation lines}
  >
  > Shipping now lands this on `dev` with those outstanding. Resolve them first (e.g. `/theshop.review {slug}`, `/theshop.verify {slug}`), or reply `proceed` to ship as-is."

  Proceed only on explicit go-ahead. **Record the waiver** in Step 7's footnote as `⚠️ waived: shipped with {N} open ledger item(s)` — carry the count from the script output. Skips stay visible, never silent.
- **No `status.md`** → record "Ledger: ⏭️ no SDD tracker for this feature" and proceed. Not blocking (the feature may predate the pipeline).

### Pre-flight 3 — There must be something to ship (halt)

```bash
git status --porcelain
git log --oneline dev..HEAD
```

If the working tree is clean **and** there are no commits on the branch ahead of `dev` (nothing to commit and nothing to merge), halt:

> "Nothing to ship — the working tree is clean and `feature/{slug}` has no commits ahead of `dev`. There's nothing to open a PR for."

---

## Steps

Each remote-mutating action is gated. If a `gh` command fails (e.g. not authenticated), stop and surface the output — suggest `! gh auth login` if it's an auth problem.

### Step 1 — Commit (if there are uncommitted changes)

If `git status --porcelain` is non-empty:

1. **Compose the message** in the repo's convention — `{Type} | {Description}` (recent history: `Feat | App bar with announcement bar...`, `Fix | Environment setup...`). Seed `{Description}` from the feature title in `.specs/{slug}/spec.md` (its `# {Title}` heading); pick `{Type}` from the nature of the change (`Feat` for a new feature, `Fix` for a fix). Append the harness `Co-Authored-By` trailer.

   Proposed message, for example:

   > `Feat | {Feature Title}`

2. **Show the proposed message and the files to be staged, and ask the user to confirm or edit it.** Do not commit until confirmed.

3. On confirmation:

   ```bash
   git add -A
   git commit -m "{confirmed message}"
   ```

If the tree was already clean (work committed earlier), skip to Step 2.

### Step 2 — Push the branch (confirm)

Confirm: *"Push `feature/{slug}` to `origin` and open a PR against `dev`?"* On yes:

```bash
git push -u origin feature/{slug}
```

Never use `--force`/`--force-with-lease` unless the user explicitly asks and explains why.

### Step 3 — Open the PR against dev

Title = the commit subject (e.g. `Feat | {Feature Title}`). Body = a short summary drawn from `.specs/{slug}/spec.md` (problem + what the feature does) plus the harness PR trailer. Then:

```bash
gh pr create --base dev --head feature/{slug} --title "{title}" --body "{body}"
```

Report the PR URL it returns. Capture the PR number for the merge step.

### Step 4 — Ask for merge

Ask exactly:

> "PR opened: {url}. Merge it into `dev` now?"

- **Yes** → merge. Default to a merge commit unless the user prefers squash/rebase:

  ```bash
  gh pr merge {#} --merge
  ```

- **No** → stop here. Leave the PR open. Skip Steps 5–6 and report the PR as open-and-unmerged in the final output.

### Step 5 — Ask to delete the branch (only after a successful merge)

Ask exactly:

> "Merged. Delete `feature/{slug}` (local and remote) now that it's on `dev`?"

- **Yes** →

  ```bash
  git push origin --delete feature/{slug}
  git branch -d feature/{slug}
  ```

  (If `gh pr merge` already deleted the remote branch via `--delete-branch`, just clean up the local one.)
- **No** → keep the branch; note it in the final output.

### Step 6 — Return to dev

```bash
git checkout dev
git pull origin dev
```

This leaves the user on an up-to-date `dev`, ready for the next `/theshop.start`.

### Step 7 — Record the outcome (if a tracker exists)

If `.specs/{slug}/status.md` exists, refresh its **Last updated** line and add a one-line footer note below the table — e.g. `**Shipped:** PR #{N} → dev ({merged YYYY-MM-DD / open})`. If you proceeded past Pre-flight 2 on open items, append the `⚠️ waived: shipped with {N} open ledger item(s)` note here too. Do not invent a new gate row; this is a footnote, not a pipeline stage.

---

## Final output

Produce this verbatim.

```markdown
# Ship report — $ARGUMENTS

| Step | Result |
|---|---|
| Commit | {SHA + subject / "nothing to commit — already committed"} |
| Push | `feature/{slug}` → origin ✅ |
| PR | {url} (→ `dev`) |
| Merge | {✅ merged / ⏸️ left open — not merged} |
| Branch | {🗑️ deleted / kept} |
| Now on | `{current branch}` @ {short SHA} |

## Ledger gate

- {✅ all stages ship-ready / ⚠️ waived: shipped with {N} open ledger item(s) / ⏭️ no tracker}

## Next step

{If merged & on dev: "Done — you're on the latest `dev`. Start the next feature with `/theshop.start <name>`."}
{If PR left open: "PR is open at {url} — merge it when ready, or re-run `/theshop.ship {slug}` to finish."}
```

---

## Rules (enforce strictly)

1. **Confirm before every remote-mutating or irreversible action.** Push, PR creation, merge, and branch deletion each require an explicit user go-ahead. No assumptions.
2. **Never merge or delete without a successful prior step.** No deletion before a confirmed merge; no merge before a created PR.
3. **Never force-push** unless the user explicitly asks and justifies it.
4. **PRs target `dev`, never `master`.** `dev` is the integration branch.
5. **Commit messages follow `{Type} | {Description}`** and carry the `Co-Authored-By` trailer; PR bodies carry the Claude Code generation trailer. Match the repo's existing history style.
6. **Never edit source or `.specs/` content.** The only `.specs/` write permitted is the `status.md` footnote in Step 7.
7. **Surface raw git/gh errors.** On any failure (auth, conflict, rejected push), stop and show the output — point at `! gh auth login` for auth issues — rather than guessing a recovery.
8. **The user may stop at any gate.** Opening a PR without merging is a valid, complete outcome — report it as such; don't push toward merge.
