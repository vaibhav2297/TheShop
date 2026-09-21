---
name: theshop-ship
description: Commit a finished feature, push it, open a PR to dev, and optionally merge and delete its branch with explicit confirmations.
argument-hint: <feature-name>
disable-model-invocation: true
---

# Ship Feature

Land `feature/{slug}` on `dev`.

Remote or irreversible actions require explicit confirmation.

## Input and branch

Read [feature identity](../theshop-start/references/feature-identity.md). Resolve input; preserve full ID as `{slug}`.

Expected branch: `feature/{slug}`.

- On `dev` or `master`: halt.
- On another feature branch: show mismatch; ask which feature to ship.
- Preserve unrelated work.

## Readiness

Always run visual gate, even without a tracker:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 visual -Feature {slug}
```

Read [visual fidelity — Verify / Ship](../theshop-plan/references/visual-fidelity.md).
Missing/stale/failing visuals block Ship. Generic `proceed` cannot waive visual gate.

When `.specs/{slug}/status.md` exists:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 ship-ready -Feature {slug}
```

Exit 1: quote every violation. Continue only after fix or explicit `proceed`. Record `⚠️ waived: shipped with {n} open ledger item(s)`.

No tracker: record `⏭️ no SDD tracker`; continue only after visual gate passes or declared backend skip.

Check:

```powershell
git status --porcelain
git log --oneline dev..HEAD
```

Clean tree plus no commits ahead of `dev`: halt—nothing to ship.

## 1. Tracker footnote

Before commit, when tracker exists:

- refresh `Last updated`
- add/update `**Shipped:** {YYYY-MM-DD} → dev (via PR)`
- append waiver text when applicable
- do not add a workflow row or PR number

This update must ride inside the feature PR.

## 2. Commit

If tree is dirty:

1. Derive title from spec; use repo style `{Type} | {Description}`.
2. Show every staged candidate and proposed message.
3. Ask for confirmation/edit.
4. Only after confirmation:
   ```powershell
   git add -A
   git commit -m "{confirmed message}"
   ```

Never include AI/model attribution.

Clean tree: use existing commits.

## 3. Push

Ask:

> Push `feature/{slug}` to `origin` and open a PR against `dev`?

On yes:

```powershell
git push -u origin feature/{slug}
```

Never force-push unless the user explicitly requests and justifies it.

## 4. Pull request

Before creation, show proposed:

- base: `dev`
- head: `feature/{slug}`
- title: commit subject
- body: short problem/change summary from spec

Ask for explicit confirmation, then:

```powershell
gh pr create --base dev --head feature/{slug} --title "{title}" --body "{body}"
```

Capture URL and number. Failure: stop and surface raw output.

## 5. Merge

Ask:

> PR opened: {url}. Merge it into `dev` now?

Yes:

```powershell
gh pr merge {number} --merge
```

No: valid endpoint. Leave PR open and stop.

## 6. Delete branch

Only after successful merge, ask:

> Merged. Delete `feature/{slug}` locally and remotely?

Yes:

```powershell
git push origin --delete feature/{slug}
git branch -d feature/{slug}
```

If remote already deleted, remove only local. Never force-delete without explicit authorization.

## 7. Return to dev

After merge:

```powershell
git checkout dev
git pull origin dev
git status --short
```

Expected final tree: clean.

## Output

```markdown
# Ship report — {slug}

| Step | Result |
|---|---|
| Commit | {SHA + subject / already committed} |
| Push | {result} |
| PR | {URL / not created} |
| Merge | {merged / left open} |
| Branch | {deleted / kept} |
| Now on | {branch + short SHA} |
| Ledger | {ready / waived count / no tracker} |

Next: {start next feature / merge open PR / exact recovery action}
```

Hard stops:

- no merge before PR
- no delete before successful merge
- PR base is `dev`, never `master`
- only tracker footnote may change under `.specs/`
- no source edits
- show raw git/gh errors; never guess recovery
- user may stop at any confirmation
