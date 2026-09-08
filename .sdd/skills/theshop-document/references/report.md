### 6. Report

End your response with this structured summary:

```
## Documentation summary

**Scope:** `git diff` — {N} files changed, {M} `.cs` / `.razor.cs` files reviewed.

**Files documented:**
- `src/TheShop.Domain/Entities/Cart.cs` — class summary + 3 method docs
- `src/TheShop.Application/Features/Cart/Commands/AddToCart/AddToCartCommand.cs` — record summary
- `src/TheShop.Application/Features/Cart/Commands/AddToCart/AddToCartHandler.cs` — class summary + Handle method
- `src/TheShop.Application/Common/Interfaces/ICartRepository.cs` — interface + 2 method docs
- `src/TheShop.Infrastructure/Persistence/Repositories/SupabaseCartRepository.cs` — class summary
- `src/TheShop.Web/Pages/Cart/CartPage.razor.cs` — class summary

**Files skipped (and why):**
- `src/TheShop.Web/Pages/Cart/CartPage.razor` — Razor markup; only the `.razor.cs` code-behind is documented.
- `tests/TheShop.Application.Tests/Features/Cart/AddToCartHandlerTests.cs` — test class, out of scope.

**Build status:** ✅ `dotnet build` succeeded with 0 warnings / 0 errors.

**Observations (for the user, not fixed by me):**
- `AddToCartHandler.Handle` does two distinct things (loads product, then mutates cart). Consider asking `shop-code-quality-review` whether to split.
- {If none, write "None."}
```

---
