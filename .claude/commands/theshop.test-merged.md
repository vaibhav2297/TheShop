---
description: Write, validate, compile, and run spec-driven unit/component tests for one feature in a single context. Produces the feature manifest, durable report, and status update without invoking test sub-agents. E2E journeys are out of scope — see /theshop.e2e.
argument-hint: <feature-name>
---

# /theshop.test-merged

**Feature requested:** `$ARGUMENTS`

Execute the complete test workflow for **The Shop** in this context:

`read -> write -> validate -> compile -> run -> analyze -> persist`

Never invoke a sub-agent or the Task tool. Read the spec and plan once, retain the resulting inventory, and avoid repeating their contents in intermediate messages.

## Scope and authority

You may:

- Read the feature artifacts, relevant source signatures, existing tests, test project files, and repository guidance.
- Create or edit feature-owned tests under `tests/TheShop.*.Tests/`.
- Overwrite `.specs/$ARGUMENTS/test-manifest.json` with the current complete test inventory.
- Overwrite `.specs/$ARGUMENTS/test-report.md` and update the Test row in `.specs/$ARGUMENTS/status.md` as specified below.
- Run the deterministic gates, builds, targeted tests, focused diagnostics, `graphify update .`, and `git rev-parse --short HEAD`.

You must not:

- Edit production code, shared E2E harness files, specs, plans, project files, package references, or dependencies.
- Derive expected behavior from production code. Use production code only to align names, signatures, and existing seams.
- Weaken, delete, skip, or comment out a valid test to obtain a green result.
- Run the full test suite.
- Write, edit, or run E2E journeys. `/theshop.e2e` owns E2E entirely — writing and running the journey in its own single context.
- Install packages or modify anything outside the paths explicitly authorized above.

If the user asks for a production fix during this command, finish the report and direct them to a follow-up workflow.

## 1. Validate input and read the sources of truth

If `$ARGUMENTS` is empty, contains a path separator, or contains `..`, stop and ask for a feature folder name. Do not write anything.

Read `.specs/$ARGUMENTS/spec.md`. It is required and is the behavioral oracle:

- Assertions and expected outcomes come from the spec.
- Every functional requirement, specified validation rule, edge case, and acceptance criterion must be represented.
- If the spec is missing, materially ambiguous, self-contradictory, or has unresolved questions that affect expected behavior, stop before writing and identify the exact blocker.

Read `.specs/$ARGUMENTS/plan.md` when present. It is the structural map:

- Use it to identify layers, types, repositories, mappers, schema/RLS contracts, external error mappings, and other technical seams.
- If plan and spec conflict on behavior, stop; the spec wins but the disagreement must be resolved before tests are written.
- If the plan is missing, proceed with behavior derivable from the spec, do not invent structural contracts, and carry a prominent degraded-coverage warning into the final report.

Use `graphify query` first for codebase discovery when `graphify-out/graph.json` exists. Otherwise use targeted `Glob`/`Grep`/`Read`. Inspect existing nearby tests and project files for real conventions and available helpers. Every architectural contract you need to write and diagnose these tests is baked into [§9](#9-architectural-context--baked-in-do-not-look-it-up) — do **not** load the `theshop.constitution` skill or any of its references, and do not copy generic test tutorials into the context.

Before editing, form a compact internal inventory of:

- Spec requirements and AC ids.
- Required Domain, Application, Infrastructure, and Web seams from the plan.
- Happy-path, validation, boundary/edge, auth/permission, and structural cases.
- Existing files to extend versus new files to create.

Do not emit this inventory as a separate report.

## 2. Write the feature tests

Follow `CLAUDE.md`, the baked-in contracts in [§9](#9-architectural-context--baked-in-do-not-look-it-up), and the conventions already demonstrated by the matching test projects. Use the packages already referenced by each test project's `.csproj`; do not add packages.

Test every layer named by the plan:

| Plan seam | Project and test style |
|---|---|
| Domain | `TheShop.Domain.Tests` — pure unit tests |
| Application | `TheShop.Application.Tests` — handlers, validators, and contracts with substitutes |
| Infrastructure | `TheShop.Infrastructure.Tests` — mappings, persistence, schema/RLS, and external error translation |
| Web | `TheShop.Web.Tests` — bUnit component/page behavior and auth presentation |

For every ordinary unit, integration, or component test:

- Name it `{MethodOrFeature}_{Scenario}_{ExpectedOutcome}`.
- Apply `[Trait("Feature", "$ARGUMENTS")]` at the method level to every `[Fact]` and `[Theory]`; never apply it to an ordinary test class.
- Take expectations from the spec and structural details from the plan.
- Cover every applicable category in the mandatory-coverage list below.
- Add a short class XML documentation comment that references `.specs/$ARGUMENTS/spec.md`.
- Preserve other features' methods when extending a shared test class.
- Keep tests deterministic and runnable; reuse existing builders and fixtures when appropriate.

### Mandatory coverage categories

Every feature gets tests in these categories — not only the cases the spec happens to enumerate. The spec sets the *expected outcome*; this list sets the *floor on scenarios*.

1. **Happy path** — at least one test per functional requirement, correct input producing the correct outcome.
2. **Validation** — for every input the feature accepts: null, empty string, whitespace, negative number, zero, out-of-range value, malformed identifier. Each must fail gracefully with the right resource key, never an unhandled exception.
3. **Edge cases** — boundary values (`0`, `1`, max, max+1), empty collections, duplicate actions, concurrent/repeated state changes, and missing related entities. Every edge case the spec names must have a test, plus the boundaries it implies.
4. **Auth guard** — applies to any feature reachable from an authenticated page or depending on `ICurrentUserService`:
   - **Admin features** (any `/admin/*` page or admin-only handler): one test that unauthenticated requests are blocked **and** one test that authenticated non-admin users are blocked. Both are required — the second is the one that is usually missed.
   - **Authenticated-user features:** a test that unauthenticated requests are blocked.
   - **Public features:** skip this category entirely; do not invent auth the spec does not imply.
5. **Structural / Infrastructure** — whenever the plan declares an Infrastructure seam. Contract from the plan, intent from the spec:
   - **Repository round-trip** — an inserted record reads back as an equal Domain entity, using the plan's column ↔ field mapping.
   - **Constraint backstop** — storage-level rules the plan specifies (e.g. a `UNIQUE(lower(email))` index rejecting a duplicate).
   - **Error translation** — each external-error → resource-key mapping in the plan (e.g. Supabase `otp_expired` → `Auth_CodeExpired`) gets a test that the adapter returns the right key.
   - Take columns, indexes, and policies from the plan. Never invent them; if the plan omits a contract you would need, flag it in the report instead of guessing.

Anything the spec or plan additionally calls out gets tests on top of these.

If a required production symbol is absent, write the test the confirmed spec requires. Do not invent a substitute production contract merely to compile; let the compile gate route the feature to implementation.

## 3. Write the manifest

Overwrite `.specs/$ARGUMENTS/test-manifest.json` with the complete current inventory of this feature's tests:

```json
{
  "feature": "$ARGUMENTS",
  "trait": "$ARGUMENTS",
  "writtenAt": "YYYY-MM-DD",
  "totalTests": 0,
  "classes": [
    { "fqn": "Namespace.ClassTests", "file": "tests/Project/Path/ClassTests.cs", "tests": 0 }
  ],
  "acceptanceCriteria": [
    { "id": "AC-1", "tests": ["Namespace.ClassTests.Method_Name"] }
  ]
}
```

Manifest rules:

- List every test class created or extended for this feature and no unrelated class.
- Count only cases carrying this feature's trait. Count one `[Fact]` as one case and each data row of a `[Theory]` as one discovered case.
- Make `totalTests` equal the sum of class counts.
- List every spec AC in order. Map it to fully-qualified test method names; use `tests: []` when uncovered rather than hiding the gap.
- For a theory, map the AC to its method name; every discovered data row for that method must pass.

## 4. Run deterministic handoff gates

Run the manifest gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 manifest -Feature $ARGUMENTS
```

If it fails, fix only the reported test/manifest violations and run it once more. If it fails again, stop with the Artifact-gate outcome below. Do not describe this state as "no tests were produced."

Run the compile gate:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/check-sdd-gates.ps1 compile -Feature $ARGUMENTS
```

Route compile failures:

- Only `[tests]` errors caused by test code: fix exactly those errors in this context and run the compile gate once more.
- Missing production symbols required by the spec: do not weaken the tests; stop with the Build-blocked outcome and direct the user to `/theshop.implement $ARGUMENTS`.
- Any `[src]` error: do not edit production; stop with the Build-blocked outcome and name the affected project/file.
- Remaining `[tests]` errors after one correction, or an unparseable build failure: stop with the Build-blocked outcome.

After the compile gate succeeds, run `graphify update .` only when both `graphify` and `graphify-out/graph.json` are available. Ignore their absence.

## 5. Run only the manifested test projects

Derive the unique `tests/<Project>/` roots from `manifest.classes[].file`, locate each root's `.csproj`, and run each project once:

```powershell
dotnet test <project.csproj> --filter "Feature=$ARGUMENTS" --no-build --logger "console;verbosity=minimal" --nologo
```

Do not run at the solution root. Aggregate each project's passed, failed, and skipped counts. The aggregate discovered count is `passed + failed + skipped`.

If output is insufficient to diagnose a failure, rerun only the failing project or test with normal/detailed verbosity. Do not rerun clean projects.

Reconcile aggregate discovered cases against `manifest.totalTests`:

- Equal: continue.
- Unequal: inspect only the listed files for missing/mistyped traits, theory-row counts, stale manifest entries, or unrelated tagged tests. Repair a clearly mechanical test/manifest defect once, rerun the manifest and compile gates as needed, then rerun only affected projects.
- If reconciliation still differs or the cause is unclear, keep the mismatch and return `Needs fixes`. Never broaden execution to whole shared classes, because that can run other features' tests.

## 6. Analyze results

Determine each AC status from the manifest and actual results:

- `Passed`: one or more mapped methods ran, and every discovered case for those methods passed.
- `Failed`: a mapped method failed, errored, or did not run.
- `Not Covered`: the manifest mapping is empty.

When every discovered test passed and reconciliation matched, all non-empty AC mappings may be treated as passed. When anything failed or did not run, obtain enough focused output to map the affected method accurately.

Scan only manifest-listed files for skipped tests, vacuous tests, improper async signatures, timing sleeps/delays, swallowed exceptions, nondeterministic time, unfinished markers, placeholder names, and assertions that verify only success without required state/side effects. Report file and line for every confirmed warning; do not flag harmless matches without inspection.

For each failure, record:

- Fully-qualified test name and layer.
- Failure classification: assertion, exception, setup, mock verification, build, or environment.
- Exact concise symptom.
- Evidence-based root-cause hypothesis, explicitly labelled a hypothesis when not proven.
- A concrete recommended file/class/method to inspect or change; never implement the production fix.

Verdict rules are strict:

- `✅ Ready`: exact reconciliation, all tests pass, no skips, every AC Passed, and no warning.
- `❌ Needs fixes`: any failure, skip, warning, mismatch, failed AC, or uncovered AC.
- `⛔ Blocked`: tests could not be authored because the input/spec was missing or unresolved.
- A compile failure is `Needs fixes — build failed`, and must say tests did not run rather than tests failed.

## 7. Persist the canonical report and status

Obtain today's date and `git rev-parse --short HEAD`; use `(unknown)` if the SHA cannot be read.

### Pre-write halt

If input/spec validation halted before test files were written, do not write `test-report.md` or change `status.md`. Emit only:

```markdown
# Test report — {feature}

**Status:** ⛔ Blocked — tests were not written.

**Reason:** {specific reason}

**Next step:** {specific resolution, then rerun `/theshop.test-merged {feature}`}
```

### Artifact gate, build gate, or completed run

For every run that wrote tests and reached a gate, overwrite `.specs/$ARGUMENTS/test-report.md`. The file and final response must be identical and use this structure:

```markdown
# Test report — {feature}

_Run: {date} · commit `{sha}` · verdict {verdict}_
_Snapshot of one run — regenerate with `/theshop.test-merged {feature}`._

## Tests written

{each changed/listed file and its feature-case count}

## Tests run

{completed metrics table, or an explicit Artifact gate / Build failed state with exact errors and "tests did not run"}

## Failures and warnings

{each actionable failure/warning with evidence; "None" when clean}

## Acceptance criteria

{one id-only row per AC with Passed, Failed, Not Covered, or Unverified}

**AC status:** {counts}

## Verdict

**{verdict}**

{one concise justification and exact next step}
```

For a completed run, the metrics table must include expected, discovered/run, reconciliation, passed, failed, skipped, pass rate, and status. For an artifact-gate failure, quote the violations and state tests were not run. For a build failure, quote compiler errors and state tests did not run. Put the complete useful diagnosis in this report; never refer to hidden agent output.

Update `.specs/$ARGUMENTS/status.md` after writing the report:

- Ready: Test State `Passing`; Gate `✅ manifest + reconciliation pass`; evidence `{discovered}/{expected} reconciled · {passedAC}/{totalAC} ACs ✅ — see [test-report.md](./test-report.md)`; Next step `/theshop.e2e $ARGUMENTS`.
- Test failure/mismatch/warning/uncovered AC: State `Failing`; Gate `🔴 {test failures | reconciliation | warnings | AC coverage}`; concise evidence plus report link; Next step names the required correction and rerun.
- Artifact gate failure: State `Failing`; Gate `🔴 manifest gate`; evidence links the report; Next step fixes the listed violations.
- Build failure: State `Failing`; Gate `🔴 build gate`; evidence names the failing project/symbol and links the report; Next step fixes production/build or runs `/theshop.implement $ARGUMENTS` for missing feature symbols.

Refresh `Last updated`. If `status.md` is missing, read only the status template section in `.claude/skills/theshop.spec/SKILL.md`, create the standard tracker, then apply the Test update.

## 8. Final integrity check

Before returning, re-read the spec, manifest, report, and status tracker and mechanically verify:

1. Manifest class counts sum to `totalTests`.
2. Manifest AC ids exactly equal the spec AC ids in order.
3. Report expected count equals manifest `totalTests`.
4. For completed runs, report discovered count and status evidence agree.
5. Report AC row/count totals agree with the manifest and spec.
6. Report verdict agrees with the status Test state and gate.
7. The persisted report is byte-for-byte the same content you will emit.

Correct a writing/count inconsistency before returning. If it cannot be corrected from available evidence, set the verdict and Test state to `Needs fixes`/`Failing`, mark `🔴 report integrity`, and state the mismatch explicitly. Never report Ready with inconsistent artifacts.

Emit the canonical report with no prose before or after it.

---

## 9. Architectural context — baked in, do not look it up

Reference material, not a step. These are the contracts that make the difference between a test that compiles and a test that is *correct* — the compile gate cannot catch a violation of any of them, so read this before writing (step 2) and before hypothesising a root cause (step 6). This section is why steps 1 and 2 forbid loading the `theshop.constitution` skill: everything needed is here.

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

Prefer `data-testid` selectors — `cut.Find("[data-testid='add-category-name']")`. This is the same hook `/theshop.e2e` uses, so a testid added for one tier serves both. MudBlazor components take it through `UserAttributes`: `UserAttributes="@(new Dictionary<string, object?> { ["data-testid"] = "…" })"`. When a page has none, use a stable alternative (localized button text via `Strings.*`) and note in the report that adding `data-testid` attributes would improve stability. For auth-guarded pages, assert the redirect: `NavigationManager.Uri.Should().EndWith("/login")`.

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
