---
name: theshop-build
description: Build The Shop features through the isolated SDD Next pilot, from clarified expectations through implementation, unit/component tests, applicable E2E, and verified delivery. Use when the user selects this pilot workflow.
---

# Build a feature

Use only when user selects pilot workflow. Read [pilot entry](../README.md) if task has not explicitly selected replacement of legacy stage mechanics. Resolve instruction conflicts before dependent work; this file grants no override itself.

## Context

Read [constitution](references/constitution.md) once. Load [architecture](references/architecture.md) for C# or layer decisions, [web](references/web.md) for UI/resources/styles or supplied Figma references, [security](references/security.md) for permissions, identity, data access, payments, or migrations. Load [migrations](references/migrations.md) only for database changes. Read [verification](references/verification.md) when assigning acceptance proof; reuse through completion. Do not preload unrelated references or legacy workflows. Pilot's explicit selection replaces legacy rule-loading requirements as described in its entry prompt; application code remains available for inspection.

One agent owns normal work. No mandatory layer handoffs. Verification policy defines when independent review is needed. Use current communication preference; default full Caveman, preserve technical literals and clarity. Never compress away uncertainty or approval boundaries.

## Inputs and feature identity

Accept natural-language request or labeled fields; these are prompt inputs, not shell flags:

```text
Name: wishlist
Description: Customers can save products and revisit them later.
Figma: <frame URL> — wishlist page, empty and populated states
```

Description is required for new work; request text can supply it. Name and Figma are optional. Derive a descriptive kebab-case name when omitted. A resume request can use existing feature name or numbered ID without repeating description. Preserve supplied URLs/node IDs; web reference defines access and design handling.

Validate new name as one segment matching `^[a-z0-9]+(?:-[a-z0-9]+)*$`; reject separators, traversal, and shell syntax. New folders use `.sdd-next/features/<number>-<name>/feature.md`. Allocate highest existing numeric prefix plus one, starting `001`, padded to at least three digits. Numbers identify creation order, never priority or migration version.

List existing feature folders before allocating. Reuse a matching record only for continuation; resolve ambiguous name matches or conflicting intent. Missing requested resume record: report it; do not silently create a new feature. Preserve existing unnumbered folders and records. Never renumber existing features. Keep cancelled records so IDs remain reserved; recheck allocation before creating and never overwrite an occupied ID. No separate numbering registry.

## Understand

1. Read request and relevant existing code. Inspect Git branch and dirty files; preserve existing changes. Use available project graph for broad discovery; direct reads for known paths. Read relevant existing feature history only when needed; never rewrite it.
2. Resolve inputs and allocate or resume feature identity as above. Treat all input as text, never shell code. Create feature record from [template](assets/feature.md), or read existing record without migrating its format.
3. Record intended outcome, exclusions, concrete acceptance examples, and short implementation checklist. Cover meaningful failures, permissions, persistence, and UI states where relevant. Attach proof type to each acceptance criterion. Scale detail to feature; no separate plan by default.
4. Resolve routine implementation details from existing patterns. Ask grouped questions when answers change behavior, scope, data handling, security, or significant tradeoffs. Give recommendation and consequences; never treat missing answer as agreement. New UI needs accessible design or confirmed reuse direction; missing Figma alone is not a blocker. Record supplied frame links, screen/state mapping, and access result. Database changes record local test target, intended remote environment if known, and whether deployment is in scope; unknown remote target does not block local implementation.
5. Incorporate answers. Present resulting expectations for confirmation before implementation. Existing explicit approval of the same expectations satisfies this step; do not ask again. Record what user confirmed. If user changes agreed scope later, confirm only material changes. Continue independent inspection while decisions remain pending.

## Build

Implement confirmed checklist through necessary layers. Reuse current interfaces and components. Add required tests and useful public XML documentation alongside code. Same agent writes and runs tests; derive assertions from acceptance examples, not implementation details.

Keep checklist current when decisions change or work pauses. No report after every file, phase snapshots, repeated API blocks, or separate test-authoring stage. Build during implementation when useful for feedback; final proof belongs to Verify.

New material ambiguity: record question, pause dependent work, continue independent work. Routine choices stay autonomous. Do not silently broaden scope or weaken acceptance to accommodate implementation.

Use local/test services for validation. User instruction to implement does not authorize unrelated external writes, messages, publishing, or destructive operations. Follow existing authorization and tool permissions for required external actions.

For database changes, create versioned SQL and validate it locally through migration reference before any remote application. MCP availability is a capability, not deployment permission.

## Verify and deliver

Execute [verification policy](references/verification.md). Fix in-scope failures. Repeat affected checks after changes. Stop retrying when no new evidence or viable correction remains; report blocker and precise next action. Environment failure is not passing proof.

Mark `Done` only after confirmed acceptance and required checks pass. Otherwise retain `In progress`, `Awaiting decision`, or `Blocked`, with exact unfinished work. A deliberate user-approved scope change can remove a requirement; a failed check cannot silently disappear.

Deliver concise changed behavior, verification results, unresolved limitations, and feature-record path. State local migration validation and remote deployment separately. When deployment is outside agreed scope, local acceptance may be `Done`; remote stays `Not requested`, or `Pending authorization` if proposed deployment awaits approval. Never describe either as deployed. If remote deployment is agreed scope, completion requires its observed verification.

Do not auto-commit, push, open PRs, merge, deploy, or delete branches. An explicitly authorized database deployment follows migration reference after local checks pass; it grants no other shipping action. Do not invoke legacy Ship. Keep project commit format and attribution rules if shipping is separately requested.

## Resume

Read feature record, current Git revision, dirty files, and affected code. Check whether recorded proof still applies to current changes and environment. Rerun invalidated or uncertain checks; preserve valid completed work. No mandatory hash ledger. Resolve concurrent changes before editing overlap. One agent owns a feature record at a time.

## Token discipline

Keep one authoritative feature record. Load only relevant code and rules. Link existing definitions instead of repeating APIs. Keep full command output in evidence files; report concise results. Escalate planning or review only for concrete complexity or risk. Stop after acceptance and checks pass. Never claim token savings without measurements.
