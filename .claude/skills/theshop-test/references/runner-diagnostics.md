## Architecture rules to check (baked in — do not look up)

When diagnosing failures and scanning for warnings, watch for violations of these project-wide rules. These come from the project's Clean Architecture. Flag them by name in your report.

### Layer dependency rule
Dependencies always point inward.

| Layer | Can depend on | Cannot depend on |
|---|---|---|
| Domain | nothing | Application, Infrastructure, Web, any SDK |
| Application | Domain | Infrastructure, Web, SDKs (`Supabase`, `Stripe`, `Resend`) |
| Infrastructure | Application, Domain | Web |
| Web | Application, Domain | Infrastructure (directly — only via DI in composition root) |

Symptoms that suggest a violation:
- A Domain test that needs mocks → Domain has external dependencies (should be pure).
- An Application test that requires a real `Supabase.Client`, Stripe key, or HTTP setup → handler is calling an SDK directly (should be behind an interface).
- A Web test that fails because `Supabase.Client` can't be resolved → a page is injecting Supabase directly (anti-pattern).
- A Domain.Tests.csproj that references `Supabase`, `Stripe`, or `MudBlazor` → wrong project reference.

### Use-case and error-handling rules
- Application handlers return `Result<T>`. They **do not** throw for expected business failures (e.g., "not found", "out of stock"). Throwing where the spec describes a graceful failure is a violation — flag it.
- Domain entities throw `DomainException` (or subtypes) for business rule violations. Returning a `Result<T>` from a Domain method is wrong — flag it.
- The `Result<T>.Error` field is a **resource key** (e.g., `"ProductNotFound"`), not a translated message. Tests asserting on translated English text mean the Application layer leaked translations.

### Presentation rules
- Pages must not contain business logic in `@code` blocks. A Web test that has to set up domain state to test page rendering suggests business logic in the page.
- Pages must inject `IMediator`, not concrete services or SDK clients. Failures involving unresolved Supabase/Stripe clients in Web tests mean the page is bypassing MediatR.
- Hardcoded English strings or hex colors in `.razor` files violate the localization and theming rules — but you won't usually catch these from test output alone. Only flag if a failure message reveals it.

### Project naming / structure
- Test projects: `TheShop.{Layer}.Tests`. A test under the wrong project (e.g., a bUnit component test in `Domain.Tests`) is a violation.
- Test naming convention: `{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`. Non-conforming names are a warning, not a failure.

---

## Common failure patterns and what they usually mean

When you see one of these signatures in `dotnet test` output, lead with the matching hypothesis in your root-cause guess. Always still label it a guess.

| Signature | Likely cause |
|---|---|
| `Expected: True, Actual: False` on `result.IsSuccess` | Handler returned `Result.Fail` when the test expected success — usually an unmet precondition (mock not set up, dependency returning null). |
| `Expected: "ProductNotFound", Actual: "Product not found"` | Handler is returning a translated message instead of a resource key. |
| `NSubstitute.Exceptions.ReceivedCallsException: Expected to receive a call ... actually received no matching calls` | Handler took a different branch than the test expected, or a required side-effect (save, send email) was skipped. |
| `System.NullReferenceException` deep in the handler | A mock dependency returned `null` and the handler didn't handle it. Either the mock setup is incomplete or the handler is missing a null guard. |
| `Bunit.ElementNotFoundException` | The component didn't render the expected element. Could be a missing `[Parameter]`, a missing service registration in `TestContext`, or a real bug in the page. |
| `Could not resolve service of type '...'` in a bUnit test | Missing `Services.AddSingleton(...)` for a dependency. Usually a test setup issue, not a product bug. |
| `Testcontainers ... Docker daemon not running` | Environment issue, not a code issue. Report and stop — don't try to "fix" Docker. |
| Build error: `The type or namespace name '...' could not be found` | Wrong using directive or missing project reference. Architecture violation if the missing namespace is from an outer layer. |

---
