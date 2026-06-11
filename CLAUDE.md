# The Shop — Project Rules

**What this project is:** A premium Canadian e-commerce web app where customers browse products, manage a cart, and checkout — with a full admin panel for managing inventory and orders. Built with .NET 10 Blazor WebAssembly + MudBlazor on the frontend, Supabase for auth and database, Stripe for payments, Resend for transactional email, hosted on Azure Static Web Apps.

Architecture and design rules live in the `theshop.constitution` skill. Load it (or its references) when generating code. The rules below are enforced on every change — violations are rejected, not committed.

---

## Non-Negotiable Rules

1. **Architecture first.** Every file goes in the correct layer (Domain / Application / Infrastructure / Web). The dependency rule is enforced by project references: `TheShop.Domain` must never import Supabase, MudBlazor, or any external SDK.

2. **MudBlazor components only.** If a UI requirement cannot be met by an existing MudBlazor component, **stop and ask** — propose an alternative and wait for confirmation. Never silently introduce custom UI primitives.

3. **No hardcoded user-facing strings.** All text comes from `Strings.resx`. Static keys via the typed `Strings.{KeyName}` accessor; `Localizer[...]` only for runtime keys (e.g. `Localizer[result.Error]`). This includes page titles, labels, validation messages, toasts, ARIA labels.

4. **No hardcoded design tokens.** Apply colors in this strict priority: `Color="Color.Primary"` → `Class="mud-*-*"` → `ShopColors.Primary` (last resort, with a comment). No hex values in `.razor` files.

5. **Always `MudText` with `Typo`.** Never use `<span>`, `<p>`, `<h1>`–`<h6>`, or any native HTML text element for content.

6. **Generate tests alongside code.** Every new handler, repository, value object, or domain method needs at least one test in the matching `tests/` project.

7. **`Shop` prefix on all theme classes.** `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. No exceptions.

---

## Workflow

- **Run the checklists.** Walk the Code Generation Checklist and Design Checklist (`references/checklists.md` in the skill) before declaring done.
- **Cite the rule.** When defending a decision, name the specific principle (e.g. "ARCHITECTURE.md §Core Principles rule 6 — Infrastructure isolates external SDKs").
- **Ask when unclear.** If layer placement, design choice, or a MudBlazor alternative is ambiguous, ask — don't guess.
- **Refuse shortcuts.** "Just put it in the page for now" / "hardcode it temporarily" — explain the cost and offer the proper path.

---

## Spec-Driven Development pipeline

The end-to-end flow for a new feature. Each step is one slash command, run in this order.

**Artifact layout.** Every feature owns one project-level folder, `.specs/{feature}/`, that groups all of its SDD artifacts: `spec.md`, `plan.md`, `test-manifest.json`, and a `status.md` pipeline tracker. The folder lives at the repo root (not under `.claude/`) so any AI agent or tool — not just Claude Code — can discover a feature's full record in one place. `status.md` shows where the feature sits in the pipeline at a glance; each step below updates its own row.

| Step | Command | What it does |
|---|---|---|
| 1. Spec | `/theshop.spec <feature>` | Non-technical product spec (WHAT/WHY) → `.specs/{feature}/spec.md`; also creates `.specs/{feature}/status.md` |
| 2. Clarify | `/theshop.clarify <feature>` | Resolves open assumptions in the **spec**, one decision at a time → flips spec Status to `Confirmed` |
| 3. Plan | `/theshop.plan <feature>` | Technical implementation plan (HOW), Figma-aware → `.specs/{feature}/plan.md` |
| 4. Resolve | `/theshop.resolve <feature>` | Resolves the **plan's** open questions / risks / assumptions (Section 11), one decision at a time → flips plan Status to `Resolved` |
| 5. Implement | `/theshop.implement <feature>` | Orchestrates four layer-scoped sub-agents (Domain → Application → Infra ‖ Web) with build gates and API handoff between phases |
| 6. Test | `/theshop.test <feature>` | `shop-test-writer` generates tests from spec → `shop-test-runner` executes them |
| 7. Verify (E2E) | `/theshop.verify <feature>` | Builds and runs the app, then smoke-checks each acceptance criterion against the running feature. **User-facing features only** — skipped for backend-only work. |
| 8. Review | `/theshop.review <feature>` | Parallel security + quality review (includes a French-localization completeness gate) with approval-gated fix-up |
| 9. Document | `/theshop.document` | Adds XML doc comments to the current diff. **Run manually** when you're ready to document the finished code — it does not auto-run. |

Formatting is automatic: a `Stop` hook in `.claude/settings.json` runs `dotnet format` whenever a turn ended with `.cs` or `.razor` changes in the diff. No manual step.

**Verification gates are scripted, not asserted.** `.claude/scripts/check-sdd-gates.ps1` deterministically validates each step's output (spec/plan template conformance, AC coverage, test-manifest integrity, layer scope, doc-only diffs, status drift); the commands above run it at entry/exit. `status.md` is the **gate ledger**: every step reads it on entry to verify the upstream rows, and records its own State + Gate + Evidence on exit. A gate the user chooses to skip is recorded as `⚠️ waived: {reason}` — never silently. Do not report a pipeline step as done while its gate fails.

---

## Common commands

```bash
dotnet build
dotnet test
dotnet run --project src/TheShop.Web
```
