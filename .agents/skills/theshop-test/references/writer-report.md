### 6. Report and update session memory

After writing the test files and the manifest, end your response with a structured summary the user can paste back to you next time:

```
## Test writing summary — {feature_name}

**Spec read:** .specs/{feature_name}/spec.md
**Plan read:** .specs/{feature_name}/plan.md  *(or: ⚠️ none found — Infrastructure/structural coverage not derived)*

**Layers covered (per plan):** Domain ✅ · Application ✅ · Infrastructure ✅ · Web ✅

**Files created/modified:**
- tests/TheShop.Domain.Tests/CartTests.cs (3 tests)
- tests/TheShop.Application.Tests/Features/Cart/AddToCartHandlerTests.cs (7 tests)
- tests/TheShop.Infrastructure.Tests/Persistence/SupabaseCartRepositoryTests.cs (2 tests)
- tests/TheShop.Web.Tests/Pages/Products/ProductDetailTests.cs (2 tests)

**Manifest written:** .specs/add-to-cart/test-manifest.json (trait `add-to-cart`, 14 tests total)

**Coverage by category:**
- Happy path: ✅ covered (FR-1, FR-2, FR-3)
- Validation: ✅ covered (negative qty, zero qty, missing product)
- Edge cases: ✅ covered (cart-max-items, duplicate add)
- Auth guard: ⚠️ N/A — public feature

**AC → Test mapping:**
- AC-1: AddToCart_WithValidProductAndQuantity_ReturnsSuccessResult
- AC-2: AddToCart_WhenItemAlreadyInCart_IncreasesQuantity
- AC-3: AddItem_WhenCartHas20Items_ThrowsDomainException

**Open questions / TODOs:**
- None.
```

This summary is your session memory. When you're invoked again for the same feature (e.g., "the spec changed, please update the tests"), the user can refer to this summary, and you can run `Glob` on `tests/**/*Tests.cs` to find what already exists rather than starting over.
