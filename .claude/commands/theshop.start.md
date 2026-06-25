---
description: Start a new feature — pull the latest dev and cut a feature/{feature-name} branch off it, so every spec artifact and code change lands on the right branch from the first commit. The git "pre" bookend of the SDD pipeline; run it before /theshop.spec.
argument-hint: <feature-name>
---

# /theshop.start

**Feature requested:** `$ARGUMENTS`

You are the **pre** git bookend for the SDD pipeline on **The Shop**. Your one job is to put the working copy on a clean `feature/{feature-name}` branch cut from the latest `dev`, so that everything the pipeline produces afterward — `.specs/{feature}/` artifacts and all source changes — is committed on that branch. You run **before** `/theshop.spec`.

You do **not** write code, generate specs, or touch `.specs/`. You run a short, deterministic git sequence with safety gates. You never force, never delete, never discard a user's work.

The `{feature-name}` you take here is the **same slug** the rest of the pipeline uses for `.specs/{feature-name}/`. The branch name and the spec folder share one identity.

---

## Input validation

If `$ARGUMENTS` is empty, stop and ask:

> "Which feature are you starting? Give me a short feature name (e.g., `add-to-cart`, `wishlist`) — I'll branch `feature/{name}` off the latest `dev`. Use the same name you'll pass to `/theshop.spec`."

Wait for the reply. Do nothing else.

### Normalize the name

Normalize exactly as `/theshop.spec` does — lowercase, hyphen-separated, alphanumerics and hyphens only; strip spaces, underscores, and special characters.

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

If the output is **non-empty**, the tree has uncommitted changes. Do **not** switch branches over a user's in-progress work — that risks carrying changes onto the wrong branch or losing them. Halt and ask:

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

   If the pull reports conflicts or fails, halt and show the output. A clean `dev` is the whole point of branching from it.

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

Run `/theshop.spec {slug}` to write the product spec — it lands on this branch.
```

---

## Rules (enforce strictly)

1. **Never switch branches over uncommitted work.** Pre-flight 1 is a hard gate — commit, stash, or get explicit instruction first.
2. **Never force, reset, or delete anything.** No `-f`, no `--hard`, no branch deletion. This is the *create* bookend; deletion is `/theshop.ship`'s concern, and only after merge.
3. **Never recreate an existing branch.** If `feature/{slug}` exists, switch to it or rename — never clobber.
4. **Never edit code or `.specs/`.** You only run git. Spec authoring is `/theshop.spec`.
5. **Branch off `dev`, not `master`.** `dev` is the integration branch; PRs target it (see `/theshop.ship`).
6. **Surface raw git errors.** On any failure, stop and show the output — don't guess at a recovery.
