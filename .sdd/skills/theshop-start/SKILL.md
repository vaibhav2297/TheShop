---
name: theshop-start
description: Start one SDD feature. Create feature/{slug} from current origin/dev. Use before $theshop-spec. Never write code or artifacts.
argument-hint: <feature-name>
---

# $theshop-start

Feature: `$ARGUMENTS`

Make clean `feature/{slug}` from latest `dev`. Same `{slug}` names `.specs/{slug}`. Next: `$theshop-spec {slug}`.

No code. No `.specs/`. No force, reset, delete, or overwrite.

## Input

Empty `$ARGUMENTS`: ask feature name. Stop.

Normalize: lowercase kebab case. Letters, digits, hyphens. State `feature/{slug}` before git changes.

## Gates

1. Run:

```bash
git status --porcelain
```

Non-empty: stop. Show changed-file summary. Ask user: commit, stash, carry work, or stop. Never switch without explicit answer.

If user says stash:

```bash
git stash push -u
```

Restore only after new branch exists.

2. Confirm `dev`:

```bash
git rev-parse --verify --quiet dev || git ls-remote --exit-code --heads origin dev
```

Missing: stop. Ask base branch. Do not choose one.

3. Confirm target absent:

```bash
git rev-parse --verify --quiet feature/{slug}
git ls-remote --exit-code --heads origin feature/{slug}
```

Existing local or remote: stop. Ask switch to it or use another name. Never recreate it.

## Create

```bash
git checkout dev
git pull origin dev
git checkout -b feature/{slug}
```

If stashed:

```bash
git stash pop
```

Any Git error: stop. Quote raw error. No recovery guess.

## Output

```markdown
# Branch ready — {slug}

- Branch: `feature/{slug}`
- Base: `dev` @ {short SHA}
- Working tree: {clean / restored stash}

Next: `$theshop-spec {slug}`.
```