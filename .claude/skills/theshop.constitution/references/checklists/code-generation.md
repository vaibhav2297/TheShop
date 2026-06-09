# Checklist — Code Generation

> Verification gate for Architecture, Tests, and Documentation rules from `SKILL.md`. Loaded by reviewer agents or when explicitly verifying code. Yes/no only — no rationale. If any answer is "no", **stop and fix it** before declaring the task complete.

---

## Architecture (Rules 1–10)

- [ ] **Rule 1.** Is this in the right layer (Domain / Application / Infrastructure / Web)?
- [ ] **Rule 1.** Does it follow the dependency rule — Domain references nothing, Application → Domain, Infrastructure → Application + Domain, Web → all (composition only)?
- [ ] **Rule 2.** Is `TheShop.Domain` free of Supabase / Stripe / MudBlazor / `[JsonProperty]` / HTTP / persistence concerns?
- [ ] **Rule 3.** Are external SDKs (Supabase / Stripe / Resend) used **only** in `TheShop.Infrastructure`? No `using Supabase;` in Domain or Application?
- [ ] **Rule 4.** Are use cases dispatched through `IMediator.Send(...)` — pages never injecting or calling repositories directly?
- [ ] **Rule 5.** Is `Result<T>` used for expected business failures? Are exceptions reserved for unexpected technical failures?
- [ ] **Rule 6.** Does business behaviour live on Domain entity methods (`cart.AddItem(...)`) — not on services, handlers, or pages?
- [ ] **Rule 7.** Are DTOs (immutable `record` types) used to cross layer boundaries? Are entities never leaking into UI?
- [ ] **Rule 8.** Does every async method that crosses a layer boundary accept `CancellationToken` and propagate it?
- [ ] **Rule 9.** Are Application features grouped by business capability (`Features/Cart/`, `Features/Checkout/`) — vertical slices, not horizontal layers?
- [ ] **Rule 9.** Are Commands / Queries / DTOs declared as `record` (immutable)?
- [ ] **Rule 10.** Are pages dumb — `@code` blocks ≤ 30 LOC, no conditional business logic in the Razor file?

## Coding standards (see `rules/architecture-core.md` §Coding standards)

- [ ] One class per file, file-scoped namespaces?
- [ ] Class / interface / Command / Query / DTO / Record / Test / Theme class names follow the naming table?
- [ ] Async methods end in `Async`? No `.Result` / `.Wait()`?
- [ ] Nullable reference types enabled? Null-forgiving `!` only when proven non-null?
- [ ] Dependencies injected via primary constructor (no backing fields) — unless the class needs validation, side effects, composition, or factory patterns?
- [ ] Collection initialisers use collection expressions (`[]`, `[a, b]`, `[..items]`) — not `new List<>()` / `new[] {}` / `Array.Empty<>`?

## Cross-cutting Web-only primitives

- [ ] Are `BusyState`, `BusyKeys`, `BusyFor`, `Routes` referenced **only** inside `TheShop.Web` — never in Application or Domain?

## Admin (only if the change touches admin)

- [ ] Admin pages live under `Pages/Admin/` and inherit layout + `[Authorize(Roles = "admin")]` from `_Imports.razor`?
- [ ] Every Supabase table holding admin-only or user-scoped data has an RLS policy?
- [ ] Customer-scoped tables filter by `customer_id = auth.uid()`?
- [ ] Admin promotion sequence runs the SQL update **and** logs the user out/in so the JWT carries the new role?

## Tests (Rule 29)

- [ ] Is there a corresponding test in the matching `tests/TheShop.{Layer}.Tests/` project for every new handler / repository / value object / domain method?
- [ ] Test name follows `{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`?
- [ ] Domain tests are pure (xUnit + FluentAssertions; no mocks)?
- [ ] Application tests use NSubstitute mocks?
- [ ] Infrastructure tests use Testcontainers (real PostgreSQL)?
- [ ] Web tests use bUnit?

## Documentation (Rule 30)

- [ ] Public types have `<summary>`?
- [ ] Public methods, properties, events with non-obvious contracts have `<summary>` / `<param>` / `<returns>` / `<exception>` where each tag adds information?
- [ ] MediatR Commands, Queries, Handlers, repository/service interfaces documented?
- [ ] Domain entity behaviours, factory methods, value object factories, domain exceptions documented?
- [ ] Summaries describe the **contract**, not the **implementation**?
- [ ] Summaries pass the anti-restatement test (not just the method name as prose)?
- [ ] No XML docs on private / internal / test code / auto-generated code / `@code` blocks in `.razor`?
- [ ] No `<remarks>` / `<example>` / `<copyright>` / `<author>` / "This class represents…" ceremony?

For design / UI / theming verification, run `checklists/design.md`.
