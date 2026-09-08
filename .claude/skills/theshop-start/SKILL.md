---
name: theshop-start
description: "Start feature branch from updated dev before spec creation. Explicit invocation only."
disable-model-invocation: true
argument-hint: "<feature-name>"
---

<!-- Generated from .sdd/skills/theshop-start/SKILL.md. Edit shared source; run sync-adapters.ps1. -->

Before writing, read `.sdd/contracts/communication.md`, `.sdd/contracts/execution.md`, and `.sdd/adapters/claude/runtime.md`. Apply shared communication policy to saved artifacts too. Resolve relative references here. Shared source above is provenance; execute rendered native instructions.

# /theshop-start

**Feature requested:** `$ARGUMENTS`

Before `/theshop-spec`, create clean `feature/{feature-name}` branch from latest `dev`. Subsequent feature artifacts and source changes belong on this branch.

## Scope

Git only. No code, specs, or `.specs/` writes. Never force, delete, or discard user work.

Use same `{feature-name}` slug for branch and `.specs/{feature-name}/`.

---

## Inputs

If `$ARGUMENTS` is empty, stop and ask:

> "Which feature are you starting? Give me a short feature name (e.g., `add-to-cart`, `wishlist`) — I'll branch `feature/{name}` off the latest `dev`. Use the same name you'll pass to `/theshop-spec`."

Wait for the reply. Do nothing else.

### Normalize the name

Normalize exactly as `/theshop-spec` does — lowercase, hyphen-separated, alphanumerics and hyphens only; strip spaces, underscores, and special characters.

- `Add To Cart` → `add-to-cart`
- `user_authentication` → `user-authentication`

The branch is then **`feature/{slug}`**. State the resolved branch name back to the user before acting on it.

---

## Pre-flight checks

Run these in order. A failure halts the flow — do not proceed to the branch step.

### Pre-flight 1 — Working tree must be clean (halt)

```bash
git status --porcelain
```

Non-empty output means uncommitted work. Halt before switching; ask:

> "Your working tree has uncommitted changes:
> {one-line summary of the changed files}
>
> Switching branches now could carry these onto the new branch or strand them. How do you want to handle it — commit them first, `git stash` them (I can pop them onto the new branch after), or are these meant to come along? I won't switch until this is resolved."

Proceed only on the user's explicit instruction. If they say stash, run `git stash push -u` and remember to `git stash pop` **after** the new branch is created.

### Pre-flight 2 — `dev` must exist (halt)

```bash
git rev-parse --verify --quiet dev || git ls-remote --exit-code --heads origin dev
```

If neither a local `dev` nor `origin/dev` exists, halt:

> "I can't find a `dev` branch locally or on `origin`. This flow branches features off `dev`. Create/publish `dev` first, or tell me which base branch to use instead."

### Pre-flight 3 — Target branch must not already exist (halt → ask)

Check both local and remote for `feature/{slug}`:

```bash
git rev-parse --verify --quiet feature/{slug}
git ls-remote --exit-code --heads origin feature/{slug}
```

If it exists anywhere, do **not** recreate or clobber it. Ask:

> "A branch `feature/{slug}` already exists ({local / on origin / both}). Do you want to switch to it as-is, or start under a different feature name? I won't reset or overwrite the existing branch."

Act only on the user's choice.

---

## Steps

Run in order. If any step errors (e.g. a pull conflict), stop and surface the raw git output — do not improvise a fix.

1. **Switch to dev:**

   ```bash
   git checkout dev
   ```

2. **Pull the latest dev:**

   ```bash
   git pull origin dev
   ```

   Pull conflict/failure: halt and show output.

3. **Cut and switch to the feature branch:**

   ```bash
   git checkout -b feature/{slug}
   ```

4. **If you stashed in Pre-flight 1**, restore the work now:

   ```bash
   git stash pop
   ```

---

## Final output

Produce this verbatim. No extra prose.

```markdown
# Branch ready — $ARGUMENTS

- **Branch:** `feature/{slug}` (cut from latest `dev`)
- **Base:** `dev` @ {short SHA after pull}
- **Working tree:** {clean / restored {N} stashed change(s)}

## Next step

Run `/theshop-spec {slug}` to write the product spec — it lands on this branch.
```

---

## Rules (enforce strictly)

1. **Never switch branches over uncommitted work.** Pre-flight 1 is a hard gate — commit, stash, or get explicit instruction first.
2. **Never force, reset, or delete anything.** No `-f`, no `--hard`, no branch deletion. This is the *create* bookend; deletion is `/theshop-ship`'s concern, and only after merge.
3. **Never recreate an existing branch.** If `feature/{slug}` exists, switch to it or rename — never clobber.
4. **Never edit code or `.specs/`.** You only run git. Spec authoring is `/theshop-spec`.
5. **Branch off `dev`, not `master`.** `dev` is the integration branch; PRs target it (see `/theshop-ship`).
6. **Surface raw git errors.** On any failure, stop and show the output — don't guess at a recovery.
