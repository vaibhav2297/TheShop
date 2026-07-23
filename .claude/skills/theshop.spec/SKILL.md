---
name: theshop.spec
description: Generate a non-technical specification document for a single feature and save it to `.specs/{feature_name}/spec.md`. The spec is product-level only — focused on WHAT the feature does and WHY it matters, never on HOW it's built. Its structure comes from `templates/spec-template.md` — six fixed numbered sections (problem statement, functional requirements, functional behaviors, constraints, edge cases and error handling, acceptance criteria), Scope and Actors & Access sub-sections inside Section 1, a Business Rules table inside Section 4, Given/When/Then acceptance criteria, and an Assumptions & Open Questions appendix that the `/theshop.clarify` skill later resolves. Blocking, load-bearing questions are asked up front; only cheap-to-change defaults are assumed and logged. This skill is manually invoked only (typically via slash command) and requires a feature name from the user; an optional `--desc <description>` may follow the name to seed the spec's content.
argument-hint: <feature-name> [--desc <description>]
disable-model-invocation: true
---

# Create Spec

Generate a non-technical feature specification and save it to `.specs/{feature_name}/spec.md`.

This skill is **manually invoked only**. It does not auto-trigger from conversational cues — the user must explicitly call it (e.g., via a slash command).

## Core principle: WHAT and WHY only — never HOW

This spec is a **product document**, not an engineering document. The audience is anyone who needs to understand what the feature does and why — product, design, QA, stakeholders. They should not need to know the codebase to read it.

Stay strictly in WHAT/WHY territory:

| Stay in (✅) | Stay out of (❌) |
|---|---|
| User actions and goals | API endpoints, request/response shapes |
| Business rules and policies | Database schemas, table designs |
| User-visible behavior and outcomes | Libraries, frameworks, tech stack |
| Time/quantity/eligibility in user terms ("within 1 business day", "for orders over $50") | Performance metrics ("200ms p95", "throughput", "memory usage") |
| What the user sees, hears, or is told | UI component names, CSS, layout markup |
| What must be true for the feature to be considered done | How code is organized, deployed, or tested |
| User-experience edge cases ("what if the cart is empty?") | Infrastructure failure modes ("what if the database is down?") |

If a section starts pulling toward implementation, rewrite it from the user's point of view. A good test: **could a non-developer product manager read this and fully understand the feature?** If no, it's gone technical.

## Inputs

The skill takes one required input — a **feature name** — and one optional input — a **free-text description** introduced by `--desc`:

```
/theshop.spec <feature-name> [--desc <description>]
```

**Parsing the invocation:**

- Everything before `--desc` is the **feature name** (e.g., `add-to-cart`, `user authentication`).
- Everything after `--desc` is the **description** — free text, no quoting needed (e.g., `/theshop.spec wishlist --desc customers can save products for later and get notified on price drops`).
- No `--desc`? The entire input is the feature name, exactly as before.
- A `--desc` with nothing after it is treated as no description.

**No feature name at all** (empty input, or input that starts with `--desc`)? Stop and ask:

> "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`) — optionally followed by `--desc` and a description of what it should do."

Wait for the reply before doing anything else. Do not invent a feature name, do not pick one from recent conversation context, and do not generate a generic template. The feature name must come from the user.

**How the description is used (when present):**

- Treat it as **authoritative product input** — it is the user pre-answering context questions. Requirements, scope hints, and behaviors stated in it go straight into the spec; don't re-ask what it already answers.
- It does **not** bypass the workflow. Still run step 2: classify the *remaining* uncertainties, ask the blocking ones, assume-and-mark the cheap ones. A description usually shrinks the question list; it never replaces it.
- If the description contains HOW-level details (libraries, schemas, endpoints), keep them **out of the spec** — acknowledge them in your reply and note they belong in `/theshop.plan`.
- If the description contradicts itself or implies two distinct features, surface that before writing (see "Keep it tight").

## Workflow

### 1. Normalize the feature name

- Filename form: lowercase, hyphen-separated, alphanumerics and hyphens only. Strip spaces, underscores, and special characters.
  - `Add To Cart` → `add-to-cart`
  - `user_authentication` → `user-authentication`
- Title form (for the document heading): keep the user's casing and spacing, or Title Case it if they gave a slug.
  - `add-to-cart` → `Add To Cart`

### 2. Gather just enough context

Before writing, take a quick pass for context — but don't turn this into a long interview.

- Glance at related files in the workspace if obviously relevant (existing specs in `.specs/`, project README, product docs).
- If one or two product-level details would meaningfully change the spec, ask focused questions. Keep questions in WHAT/WHY territory:
  - ✅ "Does this apply to logged-out users too, or only signed-in?"
  - ✅ "Should the cart persist across sessions?"
  - ❌ "Should we use localStorage or a backend session?" (that's HOW)
- **Classify every uncertainty before you resolve it — this is what decides whether you ask or assume:**
  - **Blocking / load-bearing** — the answer changes the feature's identity or scope, or guessing wrong would be expensive to reverse (e.g., "guests vs. signed-in only?", "does this touch the admin panel?", "is age-gating in scope?"). **Stop and ask up front, before writing.** These are *never* allowed to become assumptions.
  - **Resolvable default** — a sensible default exists and a wrong guess is cheap to change later (e.g., "cart persists 30 days for guests"). **Assume it, mark it inline with `(Assumption: …)`, and log it in the Assumptions & Open Questions appendix** so the user can ratify or override it. The `/theshop.clarify` skill walks that list.
  - The test: *if getting it wrong would invalidate the spec, ask; if it would change one line later, assume-and-mark.* When genuinely in doubt, ask — the appendix is for cheap defaults, not for dodging hard questions.

**Applicability checklist — consider each dimension; add a line only when it actually applies (don't force empty sections):**

- **Roles / actors** — does behavior differ for guest vs. registered customer vs. admin? If so, name who can do what. (The Shop has a full admin panel — *who is allowed* is product-level, not just implementation.)
- **Localization** — which languages must the UI support? For a premium Canadian shop, English **and** French is typically a product/legal requirement, not a detail. Note any locale-specific behavior (currency, tax wording, date/number formats).
- **Accessibility** — are there WCAG-level expectations a tester could check by hand (keyboard-reachable, screen reader announces cart/checkout changes, visible focus)? Keep it user-observable, not technical.
- **Scope boundaries** — what is explicitly **not** part of this feature? Capture it in the In Scope / Out of Scope block in Section 1. Undefined scope is the single biggest source of rework.

### 3. Write the spec using the canonical template file

**Read `.claude/skills/theshop.spec/templates/spec-template.md` and follow it exactly.** That file is the single source of truth for the spec's structure — do not reconstruct it from memory.

Structural contract (the gate enforces all of this):

- Exactly six numbered sections — don't add or drop a numbered section.
- The **Scope** (`### Scope`, with In/Out bullets) and **Actors & Access** sub-sections live inside Section 1.
- The **Business Rules** table (`### Business Rules`, `RULE-n` ids) lives inside Section 4.
- Every acceptance criterion is phrased **Given …, when …, then …** inside its `**AC-n:**` marker.
- The **Assumptions & Open Questions** appendix sits below the body, above the status footer.

Delete the template's guidance blockquotes from the generated spec — they are authoring instructions, not spec content.

### 4. Save the file

- Path: `.specs/{feature_name}/spec.md` (lowercase hyphenated folder, generic `spec.md` file name)
- Create the `.specs/{feature_name}/` directory if it does not already exist. This is the feature's home folder — its plan, test manifest, and status tracker all live here too.
- If a `spec.md` already exists in that folder, ask the user whether to **overwrite or cancel**. Never save under a different name (no `spec-v2.md` or similar) — `spec.md` is the canonical path every downstream skill and sub-agent reads; git history is the version archive (`git log -- .specs/{feature_name}/spec.md` recovers any prior revision).
- **On overwrite, downstream artifacts are stale.** A rewritten spec invalidates whatever was clarified, planned, or built from the old one. After saving, reset the downstream rows in `status.md` (Clarify, Plan, Implement, Test, Verify, Review, Document) back to `—` with a note `stale: spec rewritten {date}` in the Gate cell of any row that previously had a result. Existing `plan.md` / `test-manifest.json` files stay on disk but must be regenerated before the pipeline proceeds.
- **Run the template gate (exit gate — mandatory).** After saving, run:

  ```bash
  pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 spec -Feature {feature_name}
  ```

  The script deterministically verifies the six numbered sections, the Scope and Actors & Access sub-sections (with the In/Out-of-Scope block) in Section 1, the Business Rules sub-section in Section 4 (with `RULE-n` id sequencing when rules are present), FR/AC id sequencing, Given/When/Then phrasing in every AC, the Assumptions appendix, and that the Status footer's `N` matches the appendix count. **Exit 1 → fix the spec and re-run the gate. Never report the spec as saved while this gate fails.** Record the gate result in the status tracker (next step).

### 5. Initialize the status tracker

Write `.specs/{feature_name}/status.md` — the feature's at-a-glance SDD pipeline tracker **and gate ledger** — using the **Status tracker template** at the end of this file. Set the **Spec** row: State `Draft`, Gate `✅ spec-gate pass` (it must pass before you get here), Evidence one line of counts (e.g. `9 FRs · 12 ACs · 2 open assumptions`), today's date. Leave every later stage as `—`, and point **Next step** at `/theshop.clarify {feature_name}`. If a `status.md` already exists (e.g., the spec is being regenerated), update the Spec row rather than overwriting the whole file — and apply the downstream-row staleness reset from step 4.

### 6. Confirm

Report the saved path in one short sentence. If the spec has open assumptions, point the user at `/theshop.clarify`; otherwise just offer to refine. Examples:

> "Saved to `.specs/add-to-cart/spec.md` — 2 open assumptions logged. Run `/theshop.clarify add-to-cart` to resolve them, or tell me to tighten any section."

> "Saved to `.specs/add-to-cart/spec.md` — no open assumptions. Want me to tighten any section?"

## Spec template

The canonical template lives at **`.claude/skills/theshop.spec/templates/spec-template.md`** — read it in step 3 and follow it exactly. It is the single source of truth for the spec structure; this file intentionally does not duplicate it.

## Quality guidelines

- **Stay product-level the whole way through.** If a section drifts into endpoints, schemas, libraries, or performance numbers, rewrite it from the user's viewpoint. The reader should never have to know what language or framework the feature is built in.
- **Be specific in user/business terms.** "The cart is fast" is vague. "Items appear in the cart immediately after being added, before the user takes another action" is specific without being technical.
- **Make every requirement and acceptance criterion observable from the outside.** If you couldn't check it by clicking through the product, it's at the wrong level.
- **Cross-reference IDs when helpful.** Each acceptance criterion should map back to one or more functional requirements when the link isn't obvious (e.g., "AC-2 verifies FR-1 and FR-3").
- **Match the numbered-section count exactly.** Six numbered product sections, no more, no less. Scope lives in the In/Out block inside Section 1; assumptions live in the appendix below the status line. Neither is a numbered section, and neither counts against the six. Don't invent other top-level sections like "Future Work."
- **Keep it tight.** A useful spec is usually 1–3 pages. If it's growing past that, the "feature" is probably two features — flag this instead of writing a sprawling doc.
- **Surface assumptions inline _and_ in the appendix.** Anywhere you made a non-blocking judgment call instead of asking, mark it in place — `(Assumption: cart persists for 30 days for logged-out users.)` — and also list it in the Assumptions & Open Questions appendix so it's reviewable in one place. The inline marker is the flag; the appendix is the index `/theshop.clarify` walks. Load-bearing decisions are asked, never marked.

## Example invocations

**Example 1 — Feature name provided:**

> User: `/theshop.spec add-to-cart`
>
> Claude: [optionally asks one product-level question, e.g., "Does this need to support logged-out users, or only signed-in?"] → writes `.specs/add-to-cart/spec.md` and `.specs/add-to-cart/status.md` → "Saved to `.specs/add-to-cart/spec.md`. Want me to tighten any section?"

**Example 2 — Feature name with description:**

> User: `/theshop.spec wishlist --desc signed-in customers can save products to a wishlist and move them to the cart later; no sharing with other users`
>
> Claude: [uses the description as product input — "signed-in only" and "no sharing" land directly in scope; asks only about uncertainties the description doesn't cover] → writes `.specs/wishlist/spec.md` and `.specs/wishlist/status.md` → "Saved to `.specs/wishlist/spec.md` — 1 open assumption logged. Run `/theshop.clarify wishlist` to resolve it."

**Example 3 — No feature name provided:**

> User: `/theshop.spec`
>
> Claude: "Which feature should I create the spec for? Please give me a short feature name (e.g., `add-to-cart`, `user-authentication`) — optionally followed by `--desc` and a description of what it should do."

**Example 4 — File already exists:**

> User: `/theshop.spec add-to-cart`
>
> Claude: "A spec at `.specs/add-to-cart/spec.md` already exists. Should I overwrite it or cancel? (Overwriting marks the downstream pipeline rows stale — the previous version stays recoverable via git history.)"

## Status tracker template

Every feature carries a `.specs/{feature_name}/status.md` — a one-glance view of where it sits in the SDD pipeline **and the ledger of every verification-gate outcome**. This skill **creates** it; each later step (`/theshop.clarify`, `/theshop.plan`, `/theshop.resolve`, `/theshop.implement`, `/theshop.test`, `/theshop.verify`, `/theshop.review`, `/theshop.document`) **updates its own row** plus the **Last updated** and **Next step** lines. Use this exact structure:

```markdown
# {Feature Title} — SDD Status

**Feature:** `{feature_name}`
**Last updated:** {YYYY-MM-DD}

| Stage | State | Gate | Evidence | Date |
|---|---|---|---|---|
| 1. Spec       | Draft | ✅ spec-gate pass | {N} FRs · {M} ACs · {K} open assumption(s) | {YYYY-MM-DD} |
| 2. Plan       | —     | — | — | — |
| 3. Implement  | —     | — | — | — |
| 4. Test       | —     | — | — | — |
| 5. Verify     | —     | — | — | — |
| 6. Review     | —     | — | — | — |
| 7. Document   | —     | — | — | — |

**Next step:** `/theshop.clarify {feature_name}`
```

**Gate column vocabulary** — every step records the outcome of its verification gate(s) in its own row:

| Cell | Meaning |
|---|---|
| `✅ {gate} pass` | The step's gate(s) passed — `check-sdd-gates.ps1` modes, build gates, lint, reconciliation. |
| `🔴 {gate} fail` | A gate failed and the step ended in that state (the State cell should reflect it too). |
| `⚠️ waived: {reason}` | The step proceeded past a warning gate on the user's explicit go-ahead. Waivers are **always recorded, never silent**. |
| `—` | Stage not reached yet. |

The **Evidence** cell is one line of mechanical fact — counts, migration names, failing project, gate-script summary — never a prose claim like "looks good".

**Two pipeline-wide rules every step follows:**

1. **Read the tracker on entry.** Before doing anything, check the upstream rows are in the state you expect (e.g. `/theshop.implement` expects Plan `Resolved`). If they aren't, warn the user; if the user says proceed, record `⚠️ waived: {reason}` in your own Gate cell so the skip stays visible downstream.
2. **Record your gate on exit.** State + Gate + Evidence + Date, refresh **Last updated** and **Next step**. A step only ever writes its own row.

Stage state vocabulary (a step only ever writes its own row):

| Stage | Set by | State transition |
|---|---|---|
| 1. Spec | `/theshop.spec` → `/theshop.clarify` | `Draft` → `Confirmed` |
| 2. Plan | `/theshop.plan` → `/theshop.resolve` | `Draft` → `Resolved` |
| 3. Implement | `/theshop.implement` | `Pending` → `Done` |
| 4. Test | `/theshop.test` | `Pending` → `Passing` / `Failing` |
| 5. Verify | `/theshop.verify` | `Pending` → `Verified` / `Skipped` |
| 6. Review | `/theshop.review` | `Pending` → `Approved` / `Changes requested` |
| 7. Document | `/theshop.document` | `Pending` → `Done` |

A step that runs against a feature created before this tracker existed should create `status.md` from the template first (back-filling earlier rows as best it can from the spec/plan status footers), then set its own row.