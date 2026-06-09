---
description: Run security and quality review on the current diff in parallel for a feature, produce a unified action plan, and (only after approval) implement the fixes.
argument-hint: <feature-name>
---

# /theshop.review

**Feature requested:** `$ARGUMENTS`

You are orchestrating a parallel two-agent code review on **The Shop** project. This command has two distinct phases: a **read-only review phase** (everything before user approval) and an optional **implementation phase** (only after explicit approval). You **must not** edit any file until the user has approved the action plan in chat.

---

## Input validation

If `$ARGUMENTS` is empty (the user invoked `/theshop.review` with no feature name), stop and ask:

> "Please provide a feature name. Usage: `/theshop.review <feature-name>` — for example, `/theshop.review add-to-cart`."

Wait for the reply. Do nothing else.

---

## Pre-flight checks

Run checks. If fails halts the whole flow.

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

---

## Step 1 — Parallel review

Invoke **both** sub-agents in **a single response** via the Task tool. This is the parallelism trigger: when multiple `Task` calls go out in the same assistant turn, they execute concurrently. Do **not** call one, wait for the result, then call the other — that's sequential and violates the spec.

The two calls:

1. `subagent_type: shop-code-quality-review`
2. `subagent_type: shop-code-security-reviewer`

Both get the same prompt template:

> "Review the recently changed code for the feature `$ARGUMENTS`. Use the current uncommitted + staged diff (`git diff` and `git diff --staged`) as your scope — no need to ask the user about scope, it's already been confirmed. Follow your standard protocol and deliver your full structured report."

Wait for **both** to complete before doing anything else.

---

## Step 2 — Verify both succeeded

Inspect the two returned reports.

- If **either** agent returned an empty result, errored out, halted on its own pre-flight (e.g., couldn't read the `shop-guideline` skill files), or produced a report missing any of its required sections (`🎓 / 💡 / 🌱 / ✅`), **stop**. Do not present a unified report.

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

Two reviewers flagging *different concerns about the same place* is the most common overlap. Don't lose either perspective in the merge.

---

## Step 4 — Unified report

Produce the report in **exactly** this structure. No prose around it.

```markdown
# Code Review Report — $ARGUMENTS

## Scope

- Files changed: {N} ({list from `git diff --name-only`})
- Spec: `.claude/specs/$ARGUMENTS.md` ✅
- Reviewers run: `shop-code-security-reviewer`, `shop-code-quality-review` (parallel)

---

## Security Findings

{Full output from shop-code-security-reviewer, verbatim. Keep its 🎓 / 💡 / 🌱 / ✅ sections intact.}

---

## Quality Findings

{Full output from shop-code-quality-review, verbatim. Keep its 🎓 / 💡 / 🌱 / ✅ sections intact.}

---

## Combined Action Plan

*Ordered checklist, top to bottom = highest priority to lowest. Items merged across reviewers appear once with both perspectives noted.*

### Must fix before committing

1. **🚨 [Security – Critical]** `{file:line}` — {one-line title}
   - Why: {one sentence}
   - Action: {one sentence}

2. **⚠️ [Security – Important]** `{file:line}` — {title}
   - Why: {one sentence}
   - Action: {one sentence}

3. **💡 [Quality – Worth improving]** `{file:line}` — {title}
   - Why: {one sentence}
   - Action: {one sentence}

### Worth addressing soon

4. **[Security – Low]** `{file:line}` — {title}
   - Action: {one sentence}

5. **🌱 [Quality – Polish]** `{file:line}` — {title}
   - Action: {one sentence}

*(If a category is empty, omit its items. If both "Must fix" and "Worth addressing soon" are empty, write under the heading: "No items in the action plan — clean diff.")*

---

## Overall Verdict

**{One of three exactly}:**

- ✅ **APPROVED — ready to commit**
- 🟡 **APPROVED WITH SUGGESTIONS — can commit, address suggestions in future steps**
- 🔴 **CHANGES REQUESTED — must fix before committing, see action plan above**

{One or two sentences explaining the verdict. For 🔴, name the blocker(s). For 🟡, name the suggestions worth flagging. For ✅, briefly say what was done well.}
```

### Verdict selection rules (be strict)

Pick the verdict using only these rules — no judgment calls:

| Condition | Verdict |
|---|---|
| Any 🚨 Critical security finding, OR any ⚠️ Important security finding, OR any 💡 Quality "Worth improving" finding | 🔴 **CHANGES REQUESTED** |
| No items above, BUT at least one unmarked security finding, OR at least one 🌱 Quality "Polish" item | 🟡 **APPROVED WITH SUGGESTIONS** |
| Nothing in any "must fix" or "worth addressing" bucket — only ✅ Doing well | ✅ **APPROVED** |

Critically: even one critical security finding overrides everything else and forces 🔴 — regardless of how many ✅ items the reviewers found.

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
- **If a finding turns out to need spec changes** (the spec disagrees with the code, and the code is actually right), don't edit the spec automatically. Flag it and tell the user to re-run `/create-spec $ARGUMENTS` or update the spec by hand.

When implementation is finished, produce a short summary:

```markdown
## Implementation summary

**Applied:**
- ✅ Action #1: `{file:line}` — {what was changed in one line}
- ✅ Action #2: `{file:line}` — {one line}

**Skipped (needs your input):**
- ⏭️ Action #N: `{file:line}` — {why I stopped: design decision, ambiguous scope, etc.}

**Next step:** Re-run `/theshop.review $ARGUMENTS` to verify the fixes, or `/theshop.test $ARGUMENTS` to make sure nothing broke.
```

---

## Rules (enforce strictly)

1. **Do not edit any file before explicit user approval in chat.** No exceptions. Even if a fix is "obvious," even if it's a one-line typo. The approval gate is the contract.
2. **Do not start one reviewer before the other.** Both `Task` calls go out in the same response. Sequential invocation is a bug.
3. **Do not skip the pre-flight checks.** No spec → halt. No diff → halt. Both pre-flights run before any reviewer is invoked.
4. **Do not proceed if the spec file doesn't exist.** Report and stop. The spec gate is intentional.
5. **Do not present a partial review as complete.** If either reviewer fails, no unified report is produced — just an error report and stop.
6. **Do not edit any file under `.claude/skills/shop-guideline/` or `.claude/specs/*` in the implementation phase.** The skill's rules, references, examples, and checklists are governing documents — changes to them belong in a separate, deliberate flow, not in a quick fix loop.
