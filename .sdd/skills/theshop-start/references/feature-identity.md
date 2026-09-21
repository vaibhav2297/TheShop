# Feature identity

New IDs: `NNN_feature-name`. Example: `005_manage-product`.
Positive number; minimum three digits. Name: lowercase kebab case. Preserve underscore separator.

Same full ID: `.specs/{id}/`, `feature/{id}`, status, manifest `feature`/`trait`, test traits/filters,
design contract, evidence, next commands. C# types stay semantic; never prepend numbers.
Files remain `spec.md`, `plan.md`, `status.md`. Spec/plan metadata: `**Feature:**` plus full ID.

## Start: choose next number

After clean-tree/base checks, fetch refs. Fetch failure blocks numbering.

```powershell
git fetch origin
python .sdd/scripts/feature_identity.py next "{feature-name}"
```

Returned ID becomes `{slug}`. Number: maximum numbered spec folder/local branch/fetched remote branch plus one.
No numbered features: `001`. Never fill gaps. Legacy count does not affect sequence.
Explicit number must equal next number. Duplicate name/number fails; resolve existing work.

Before branch creation: refresh refs, rerun `next`, recheck target absence. Use latest result.
Helper is read-only. Branch creation reserves ID locally. Parallel clones can collide before publishing.
Collision: stop; resolve identity before artifacts. Never silently renumber existing work.
Never delete highest-numbered spec history to recycle number. No global allocator added.

## Spec and later stages: resolve, never allocate

```powershell
python .sdd/scripts/feature_identity.py resolve "{feature-name-or-id}"
```

Use stdout literally for folder placeholders `{feature}`, `{slug}`, `{file_name}`. Preserve prefix.
Exact existing ID wins. Plain name resolves only when unique. Unknown/ambiguous: full ID or Start required.
Spec can resolve branch ID before folder exists. Never rerun `next` after Start.
Never insert IDs into C# placeholders such as `{Feature}Command`.

## Compatibility

Existing unnumbered folders/branches keep IDs. No automatic rename, chronology inference, manifest rewrite or link changes.
New unnumbered specs fail spec gate. Unnumbered specs already present in `HEAD` remain valid.
