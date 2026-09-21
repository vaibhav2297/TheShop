---
name: theshop-document
description: Optionally add XML documentation to changed C# APIs without behavioral edits.
argument-hint: [feature-name]
disable-model-invocation: true
---

# Document Diff

Optional, single-session documentation pass. Never blocks shipping.

Add or improve XML comments only. No sub-agents.

## Scope

Inspect:

```powershell
git diff --name-only
git diff --staged --name-only
```

No changed files: halt. If a commit range was explicitly supplied, use that range.

List the C# files in scope. Preserve unrelated edits.

Load `$theshop-constitution` documentation guidance.

## Baseline

Before editing:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 snapshot -Snapshot "$env:TEMP/theshop-doc-snapshot"
```

## Edit

Document changed public/protected APIs where comments add value:

- purpose and responsibility
- parameters whose meaning is not obvious
- return semantics
- exceptions actually thrown
- important invariants or side effects

Use valid XML tags. Match repository style.

Do not:

- change behavior, signatures, names, visibility, formatting, imports, or ordering
- document private/self-evident members mechanically
- invent guarantees
- touch non-`.cs` files
- add generated or boilerplate prose

## Gates

Run:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .sdd/scripts/check-sdd-gates.ps1 doc-only -Snapshot "$env:TEMP/theshop-doc-snapshot"
dotnet build TheShop.slnx --nologo
```

`doc-only` failure: stop. Show exact non-comment changes. Do not silently revert or bless them.

Build failure: report it. Do not repair production behavior here.

## Tracker

Feature supplied: read [feature identity](../theshop-start/references/feature-identity.md).
Resolve full ID before tracker access. Never strip number.

Follow `.sdd/README.md` artifact writing style for SDD prose.

Only when a feature name was supplied and both gates pass:

- Document: `Done`
- Gate: `✅ doc-only gate + build pass`
- Evidence: files/members documented
- Date today; refresh `Last updated`
- Next: `$theshop-ship {feature}`

No feature name: do not touch a tracker.

## Output

Return:

- files inspected
- files/members documented
- skipped items and reason
- doc-only gate
- build
- next: `$theshop-ship {feature}` when tracked

Never claim success if any non-documentation delta was introduced.
