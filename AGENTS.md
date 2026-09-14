# The Shop — Project Rules

## SDD Next

`.sdd/theshop-build/SKILL.md` is the sole feature workflow. Use `$theshop-build` in Codex and `/theshop-build` in Claude Code for feature work. It owns Understand, Build, Verify, Deliver, and records new work in `.sdd/features/`.

Read canonical skill and relevant references before feature work. Confirm material behavior, scope, data, security, or design decisions before dependent implementation. Existing explicit approval of same expectations remains valid. Do not create a feature record for unrelated questions or general planning-only requests. Explicit `Mode: understand` requests a feature record and stops before implementation, even after confirmation. `Mode: implement` resumes an existing confirmed record through Build, Verify, Deliver. Omitted mode preserves full flow; model selection stays in runtime.

Use one feature record and one normal implementation owner. Risk-based independent review is required for authorization, identity, payments, sensitive access, and destructive database changes. A missing independent reviewer leaves proof pending.

Do not auto-commit, push, open a pull request, merge, deploy, or delete branches. Git and remote actions require explicit authorization. Use `{Type} | {Description}` for any authorized commit. Do not include AI or agent attribution in commits, pull requests, or reviews.

`.specs/` contains inactive historical SDD records. Preserve them unchanged. Continuing historical work creates a new Next record that links source record and names remaining acceptance; never convert waived or failed checks into passes.

## Engineering rules

The Shop is a .NET 10 Blazor WebAssembly application using MudBlazor, Supabase, Stripe, Resend, and Azure Static Web Apps.

1. Put every file in Domain, Application, Infrastructure, or Web. Domain never imports Supabase, MudBlazor, Stripe, Resend, or another external SDK.
2. Use MudBlazor components only. If no suitable component exists, stop and request an approved alternative.
3. Put all user-facing text in `Strings.resx`. Use typed `Strings.{Key}` for static keys and `Localizer[...]` only for runtime keys.
4. Do not hardcode design tokens. Prefer `Color="Color.Primary"`, then `mud-*-*` classes, then commented `ShopColors` fallback. Do not use hex colors in `.razor`.
5. Use `MudText` with `Typo` for content text. Do not use native text elements.
6. Add matching tests for every new handler, repository, value object, or domain method.
7. Prefix theme classes with `Shop`: `ShopColors`, `ShopIcons`, `ShopTypography`, and `ShopTheme`.
8. Keep user-facing commit, pull request, and review text free of AI or agent attribution.
9. Prefer XML documentation comments for public C# types and members. Use implementation comments only when XML documentation cannot express reason.
10. Use `{Type} | {Description}` for authorized commits.

For C# and Razor work, load `.sdd/theshop-build/references/constitution.md` and relevant architecture, web, security, migration, and verification references. Run scoped `dotnet format TheShop.slnx --no-restore --include <paths>` for owned C# or Razor edits, then repeat affected checks. Run `.sdd/scripts/check-design-rules.ps1 -Path <changed-production-files>` for changed production C# or Razor files. Build with `dotnet build TheShop.slnx --nologo`; run applicable tests and browser proof. A failed, skipped, blocked, stale, or unavailable check is not passing proof.

## Graphify

When `graphify-out/graph.json` exists, query Graphify before broad codebase discovery. Use `graphify query` for context, `graphify path` for relationships, and `graphify explain` for a focused concept. After code changes, run `graphify update .`. Graph results guide inspection; they do not replace source review or verification.

## Communication

Default Caveman level is `full`; explicit user choice overrides it. Preserve technical literals, facts, authorization boundaries, and uncertainty.
