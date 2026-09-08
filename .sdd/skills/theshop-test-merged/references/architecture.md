## 9. Architectural context — baked in, do not look it up

Reference material, not a step. These are the contracts that make the difference between a test that compiles and a test that is *correct* — the compile gate cannot catch a violation of any of them, so read this before writing (step 2) and before hypothesising a root cause (step 6). This section is why steps 1 and 2 forbid loading the `theshop-constitution` skill: everything needed is here.

### Layers and namespaces

- `TheShop.Domain` — entities, value objects, enums, domain exceptions. Pure C#, no external dependencies.
- `TheShop.Application` — MediatR commands/queries + handlers, interfaces for external dependencies, DTOs, validators, `Result<T>`.
- `TheShop.Infrastructure` — concrete implementations of Application interfaces (Supabase, Stripe, Resend).
- `TheShop.Web` — Blazor pages, components, state stores, MudBlazor UI.

### Per-layer tooling

| Layer | Test type | Tools |
|---|---|---|
| Domain | Pure unit tests — no mocks | xUnit, FluentAssertions |
| Application | Mocked unit tests — substitute every interface dependency | xUnit, NSubstitute, FluentAssertions |
| Infrastructure | Integration tests — real Postgres via Testcontainers | xUnit, Testcontainers.PostgreSql, FluentAssertions |
| Web | Component tests — substitute every injected service | xUnit, bUnit, NSubstitute, FluentAssertions |

All four sets are already referenced by the test projects. Do not add packages.

### Error contracts — the highest-value rules here

- **Domain** throws. Business-rule violations raise `DomainException` or a subtype (`InsufficientStockException`, …). Assert with `.Should().Throw<DomainException>()`. A Domain method returning `Result<T>` is a violation — flag it.
- **Application** returns, never throws for expected failures. Handlers are `IRequestHandler<TRequest, Result<TResponse>>`. A handler that throws where the spec describes a graceful failure is a violation — flag it.
- **`Result<T>` semantics:**
  - `Result.Ok(value)` → `IsSuccess == true`, `Value` populated, `Error == ""`.
  - `Result.Fail(errorKey)` → `IsSuccess == false`, `Value == null`, `Error` is a **resource key string** such as `"ProductNotFound"` (produced by `nameof(Strings.ProductNotFound)`).
- **Assert against the resource key, never the translated message.** `result.Error.Should().Be("ProductNotFound")` is correct; `.Be("Product not found")` means the Application layer leaked a translation and the test enshrines the leak. This is the single most common silent defect in this repo's tests.

### Application seams to substitute

`IProductRepository`, `ICartRepository`, `IOrderRepository`, `ICustomerRepository`, `IPaymentService`, `IEmailSender`, `IAuthService`, `ICurrentUserService`, `IMapper` (AutoMapper).

Substitute **every** constructor dependency. `Substitute.For<T>()` + `.Returns(…)` to set up, `.Received(N)` to verify, `Arg.Any<CancellationToken>()` for tokens, specific values where they matter.

`ICurrentUserService` in tests:

```csharp
_user.IsAuthenticated.Returns(true); _user.Id.Returns(userId);   // authenticated
_user.IsAuthenticated.Returns(false);                            // unauthenticated
```

Admin representation depends on the auth model — take it from the spec or plan; do not assume.

### Validators

Test validators directly via `FluentValidation.TestHelper`:

```csharp
new AddToCartValidator().TestValidate(command).ShouldHaveValidationErrorFor(x => x.Quantity);
```

Validators run through a MediatR `ValidationBehavior` pipeline in production — do not try to exercise that pipeline from a unit test.

### Web / bUnit

Pages inject `IMediator`, `CartState`, `AuthState`, `ToastState`, `ISnackbar`, `IStringLocalizer<Strings>`, and `NavigationManager`. Register **all** of them in the bUnit `TestContext` — a page throws on any missing registration — and always include `Services.AddMudServices()`; MudBlazor components fail without it.

Prefer `data-testid` selectors — `cut.Find("[data-testid='add-category-name']")`. This is the same hook `{{command:theshop-e2e}}` uses, so a testid added for one tier serves both. MudBlazor components take it through `UserAttributes`: `UserAttributes="@(new Dictionary<string, object?> { ["data-testid"] = "…" })"`. When a page has none, use a stable alternative (localized button text via `Strings.*`) and note in the report that adding `data-testid` attributes would improve stability. For auth-guarded pages, assert the redirect: `NavigationManager.Uri.Should().EndWith("/login")`.

### Infrastructure

Share the container with `IClassFixture<PostgresFixture>` — never one container per test — and reset state between tests inside the class. These tests verify the record ↔ entity mapping and the storage-level constraints from the plan; they do **not** re-test business rules, which belong to Domain tests.

### Dependency rule — for diagnosing failures in step 6

| Layer | May depend on | Must not depend on |
|---|---|---|
| Domain | nothing | Application, Infrastructure, Web, any SDK |
| Application | Domain | Infrastructure, Web, SDKs (`Supabase`, `Stripe`, `Resend`) |
| Infrastructure | Application, Domain | Web |
| Web | Application, Domain | Infrastructure directly — only via DI in the composition root |

Failure shapes that indicate a violation: a Domain test that needs mocks (Domain should be pure); an Application test needing a real `Supabase.Client`, Stripe key, or HTTP setup (handler is calling an SDK directly instead of through an interface); a Web test failing because `Supabase.Client` cannot be resolved (a page is injecting Infrastructure directly); a `TheShop.Domain.Tests.csproj` referencing `Supabase`, `Stripe`, or `MudBlazor`. Also flag business logic in a page's `@code` block, and a page injecting concrete services instead of `IMediator`. When a failure points at one of these, name the violated rule in the failure record.

### Common failure signatures

Lead with the matching hypothesis, still labelled a hypothesis.

| Signature | Likely cause |
|---|---|
| `Expected: True, Actual: False` on `result.IsSuccess` | Handler returned `Result.Fail` where the test expected success — usually an unmet precondition or a substitute that was never configured. |
| `Expected: "ProductNotFound", Actual: "Product not found"` | Handler returned a translated message instead of the resource key. |
| `ReceivedCallsException: … actually received no matching calls` | Handler took a different branch, or a required side effect (save, send) was skipped. |
| `NullReferenceException` inside the handler | A substitute returned `null` and the handler has no guard — incomplete setup, or a missing null check in production. |
| `Bunit.ElementNotFoundException` | Component did not render the expected element: missing `[Parameter]`, missing service registration, or a real page bug. |
| `Could not resolve service of type '…'` in a bUnit test | Missing `Services.AddSingleton(…)` or `AddMudServices()` — a test-setup defect, not a product bug. |
| `Testcontainers … Docker daemon not running` | Environment failure. Report it and stop; classify as environment, not assertion. |
| `The type or namespace name '…' could not be found` | Wrong `using` or missing project reference — an architecture violation if the namespace belongs to an outer layer. |
