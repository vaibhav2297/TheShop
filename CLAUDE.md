# The Vape Shop — Project Instructions

You are working on a premium Canadian vape e-commerce platform.

**Stack:** .NET 10 Blazor WebAssembly + MudBlazor + Supabase + Stripe + Resend, hosted on Azure Static Web Apps.

---

## Required reading

Before generating, modifying, or reviewing any code, you MUST read both of these files in full:

- @ARCHITECTURE.md — Clean Architecture rules, layer separation, folder structure, naming conventions, dependency rules, testing strategy, and coding standards.
- @DESIGN.md — Visual language, theming, MudBlazor usage rules, color hierarchy, typography rules, icon registry, and string localization.

These two files are the project's source of truth. Treat them as a contract that overrides any conflicting instructions given in conversation.

---

## Critical rules — non-negotiable

1. **Architecture first.** Every file you create or modify must go in the correct layer (Domain / Application / Infrastructure / Web) per `ARCHITECTURE.md`. The dependency rule is enforced — code in `TheShop.Domain` must never import Supabase, MudBlazor, or any external SDK.

2. **MudBlazor components only.** If a UI requirement cannot be met by an existing MudBlazor component, STOP and ask the user. Propose at least one alternative and wait for explicit confirmation before implementing anything custom. Never silently introduce custom UI primitives.

3. **No hardcoded user-facing strings.** All text the user reads must come from `Strings.resx` via `IStringLocalizer<Strings>`. This includes page titles, button labels, validation messages, toast messages, and ARIA labels.

4. **No hardcoded design tokens.** Apply colors using this strict priority order: `Color="Color.Primary"` first → `Class="mud-theme-primary"` second → `ShopColors.Primary` only as a last resort with a comment explaining why.

5. **Always `MudText` with `Typo`.** Never use `<span>`, `<p>`, `<h1>` through `<h6>`, or any other native HTML text element to display content.

6. **Generate tests alongside code.** Every new handler, repository, value object, or domain method needs at least one test in the matching `tests/` project.

7. **Use the `Shop` prefix.** All theme-related classes must be named `ShopColors`, `ShopIcons`, `ShopTypography`, `ShopTheme`. No exceptions.

---

## Workflow expectations

- **Run the checklists.** Before treating any task as complete, mentally walk through the Code Generation Checklist in `ARCHITECTURE.md` and the Design Checklist in `DESIGN.md`. If any item fails, fix it before declaring done.
- **Cite the rules.** When explaining a design or architecture choice, reference the specific principle (e.g. "per ARCHITECTURE.md §Core Principles rule 6, Infrastructure isolates external SDKs").
- **Ask when unclear.** If a request is ambiguous about layer placement, design choice, or whether a MudBlazor alternative is acceptable, ask before generating code rather than guessing.
- **Refuse to violate rules.** If asked to "just put it in the page for now" or "hardcode it temporarily", explain why this creates technical debt and offer the proper approach. Short-term shortcuts compound quickly in a Clean Architecture project.

---

## Common commands

```bash
dotnet build                                       # build the solution
dotnet test                                        # run all tests
dotnet run --project src/TheShop.Web              # start the dev server
dotnet new sln -n TheShop                         # create the solution (initial setup)
```

---

## When `ARCHITECTURE.md` or `DESIGN.md` is updated

Run `/compact` in the current session, or start a new session. Claude Code will re-read both files fresh and apply any changes immediately.
