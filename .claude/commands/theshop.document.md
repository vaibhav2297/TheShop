---
description: Add XML doc comments to the current diff using the shop-code-documenter sub-agent. Standalone wrapper for users who want to document existing changes without running the full /theshop.implement flow.
argument-hint: (no arguments)
---

# /theshop.document

You are a thin wrapper that invokes the `shop-code-documenter` sub-agent for the current diff. This command exists so users can add XML doc comments to changes that weren't produced by `/theshop.implement` — e.g., a manual refactor, a hand-written addition, or a feature implemented before this workflow existed.

The full implementation pipeline (`/theshop.implement`) already runs the documenter as its final phase, so for new features started through the pipeline, you don't need this command.

---

## Workflow

### 1. Pre-flight — diff must be non-empty

Run:

```bash
git diff --name-only
git diff --staged --name-only
```

If both are empty, halt:

> "Working tree is clean and no staged changes. I document recently changed code — make changes first (or specify a commit range) and re-invoke me."

If a diff exists, briefly list the changed files (output of the two commands above) so the user can confirm scope before the documenter starts.

### 2. Invoke `shop-code-documenter`

Call the sub-agent via the Task tool, `subagent_type: shop-code-documenter`. Prompt:

> "Add XML doc comments to the current diff (`git diff` + `git diff --staged`). Follow your standard protocol per `references/rules/documentation.md` and end with the structured summary."

Wait for it to complete.

### 3. Relay the documenter's report

Pass the documenter's structured summary back to the user verbatim. Do not add commentary on top — the documenter's report is already in the agreed format and the user will read it directly.

If the documenter reported a build failure or halted on a question, surface that prominently above the summary:

> "⚠️ Documenter halted — see report below for the reason."

---

## Hard rules

1. **Do not edit any file yourself.** Delegate to the sub-agent. This command is orchestration-only.
2. **Do not invoke any other agent.** Quality, security, tests, implementers — all out of scope. If the user wants those, they'll run the dedicated commands.
3. **Do not loop the documenter.** One invocation per command run. If it fails, surface the failure; don't retry.
