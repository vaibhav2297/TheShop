## Report format

Read `.sdd/contracts/test-proof.md` before classification or reporting. It defines Passed, Deferred, Failed, and Not Covered, including the stage-specific `Ready for E2E — deferred proof remains` verdict. Deferred ACs stay outside passed counts; supporting tests still must pass.


Always deliver the report in this exact structure. Markdown rendered, no extra prose around it.

```markdown
# Test run report — {feature_name}

## 1. Summary

| Metric | Value |
|---|---|
| Expected (manifest) | 12 |
| Discovered/run | 12 |
| Reconciliation | ✅ matched |
| Passed | 10 |
| Failed | 2 |
| Skipped | 0 |
| Pass rate | 83.3% |
| Duration | 4.7s |
| Status | 🔴 Red |

**Filter used:** `Feature=add-to-cart`

**Classes executed (from manifest):**
- TheShop.Domain.Tests.CartTests
- TheShop.Application.Tests.Features.Cart.AddToCartHandlerTests
- TheShop.Web.Tests.Pages.Products.ProductDetailTests

*(If reconciliation did not match, replace the row value with e.g. `🔴 mismatch — 9 run vs 12 expected` and add a "Reconciliation" note below this table naming the missing or extra classes.)*

## 2. Acceptance criteria

One row per AC from the manifest's `acceptanceCriteria`, referenced by **id only** (the spec owns the wording).

| AC | Status | Covered by |
|---|---|---|
| AC-1 | ✅ Passed | `Handle_WithValidProductAndQuantity_ReturnsSuccessResult` |
| AC-2 | ✅ Passed | `AddItem_WhenItemAlreadyInCart_IncreasesQuantity` |
| AC-3 | ❌ Failed | `AddItem_WhenCartHas20Items_ThrowsDomainException` |
| AC-4 | ⚠️ Not Covered | — |

**AC status:** {N} passed · {N} failed · {N} not covered

*(Status legend: ✅ Passed — all mapped tests ran and passed · ❌ Failed — a mapped test failed or didn't run · ⚠️ Not Covered — no test maps to this AC. Any ❌ or ⚠️ forces 🔴 NOT READY. If no AC oracle was available, replace this table with: "AC verification not performed — no `acceptanceCriteria` in the manifest and no `// AC → Test mapping` footer found.")*

## 3. Failures (deep dive)

### ❌ Failure 1 — `TheShop.Application.Tests.Features.Cart.AddToCartHandlerTests.Handle_WhenProductNotFound_ReturnsFailureResult`

- **Layer:** Application
- **Failure type:** Assertion failure
- **Symptom:** `Expected result.Error to be "ProductNotFound", but found "Product not found".`
- **Root-cause hypothesis (guess):** The handler is returning a translated English message instead of the resource key. The contract is that `Result<T>.Error` holds a key like `nameof(Strings.ProductNotFound)`, which the Web layer translates via `IStringLocalizer`.
- **Rules violated:** Application must return resource keys, not translated strings.

### ❌ Failure 2 — `...`
*(same structure)*

## 4. Warnings & flags

- ⚠️ `tests/TheShop.Application.Tests/Features/Cart/AddToCartHandlerTests.cs:84` — `[Fact(Skip = "flaky")]` on `Handle_WhenConcurrentAdd_StillSucceeds`. Test silently disabled.
- ⚠️ `tests/TheShop.Web.Tests/Pages/Products/ProductDetailTests.cs:42` — `Thread.Sleep(500)` inside test body. Likely race-condition workaround; flaky in CI.
- *(empty if none — but always include the section header)*

## 5. Recommendations

1. **In `src/TheShop.Application/Features/Cart/Commands/AddToCartHandler.cs`:** change the not-found branch to `return Result.Fail<CartDto>(nameof(Strings.ProductNotFound));` instead of returning the translated message. Fixes Failure 1.
2. **Re-enable the skipped concurrency test** once the cause is understood. Either fix the underlying race or add the proper synchronization in the test — don't leave it skipped indefinitely.
3. *(...)*

## 6. Verdict

**🔴 NOT READY**

2 of 12 tests are failing and AC-3 is failing while AC-4 has no covering test. The feature is not done until every test passes **and** every acceptance criterion is ✅ Passed — fix Failures 1 and 2, and add a test for AC-4 (or formally move it out of scope in the spec).
```

### Build-failure variant (Step 2 gate tripped)

When the build gate fails, **replace the entire report above** with this trimmed form. No five-layer analysis, no per-test breakdown, no warning scan — none of it applies to code that didn't compile.

```markdown
# Test run report — {feature_name}

## 1. Summary

| Metric | Value |
|---|---|
| Build | 🔴 Failed — tests did not run |
| Tests discovered | 0 (build blocked the run) |
| Status | 🔴 Red |

**Build errors:**
- `{project}` — `{file}:{line}` — `error CSxxxx`: {message}
- ...

{If the failing project is NOT one the manifest lists for this feature, add: "⚠️ The break is in `{project}`, which is not part of this feature's test set — `{feature_name}`'s own tests are blocked by an unrelated compile error."}

## 2. Acceptance criteria

Not verified — the solution did not compile, so no acceptance criterion could be exercised. Every AC stays ⚠️ unverified until the build is green.

## 3. Recommendations

1. Fix the compile error(s) listed above, then re-run me. {Point at the specific file/symbol when the cause is clear; label any inference as a guess.}

## 4. Verdict

**🔴 NOT READY — build failed**

The solution does not compile, so none of `{feature_name}`'s tests ran and no acceptance criterion is verified. This is a "tests did not execute" state, not a test failure — fix the build error(s) and re-run.
```

Verdict rules:
- 🟢 **READY** — 100% pass, reconciliation matched (every test the manifest promised ran, and nothing extra), **every acceptance criterion ✅ Passed**, no skipped tests, no warnings flagged. Passing tests alone are not enough: an AC left ⚠️ Not Covered or ❌ Failed can never be READY.
- 🟡 **READY WITH CAVEATS** — 100% pass, reconciliation matched, and every AC ✅ Passed, but at least one warning (skips, flakes, sneaky issues). Always name the caveat.
- 🔴 **NOT READY** — any failure, build error, test-infrastructure failure, a reconciliation mismatch, **or any acceptance criterion ❌ Failed or ⚠️ Not Covered**. A mismatch is NOT READY even if every test that ran passed, because coverage is unverified — name the missing/extra classes. An uncovered or failing AC is NOT READY even if every test that ran passed — name the AC id.

The verdict is one sentence on the headline line, plus 1–3 sentences explaining what would change it to green. State explicitly that READY requires both all tests passing **and** all acceptance criteria passing.

---
