# The Shop — Project Rules

**What this project is:** A premium Canadian e-commerce web app where customers browse products, manage a cart, and checkout — with a full admin panel for managing inventory and orders. Built with .NET 10 Blazor WebAssembly + MudBlazor on the frontend, Supabase for auth and database, Stripe for payments, Resend for transactional email, hosted on Azure Static Web Apps.

Architecture and design rules live in the `shop-guideline` skill. Load it (or its references) when generating code. The rules below are enforced on every change — violations are rejected, not committed.

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

## Common commands

```bash
dotnet build
dotnet test
dotnet run --project src/TheShop.Web
```
