### 9. Report the produced surface

End your response with this structured summary:

```
## Infrastructure implementation summary — {feature_name}

**Plan sections read:** 4 (tables), 7 (Phase 3), 10 (Schema + RLS) of `.specs/{feature_name}/plan.md`

**Files created/modified:**
- `src/TheShop.Infrastructure/Persistence/Records/CartRecord.cs` (new)
- `src/TheShop.Infrastructure/Persistence/Records/CartItemRecord.cs` (new)
- `src/TheShop.Infrastructure/Persistence/Mappers/CartMapper.cs` (new)
- `src/TheShop.Infrastructure/Persistence/Repositories/SupabaseCartRepository.cs` (new)
- `src/TheShop.Infrastructure/DependencyInjection.cs` (1 new registration)

**Migration applied:**
- Name: `add_cart_tables`
- Tables: `carts`, `cart_items`
- Indexes: `idx_cart_items_cart_id`
- RLS: enabled on both tables; policies `carts_customer_access`, `cart_items_customer_access`, `carts_admin_select` all applied.

**Interfaces implemented:**
- `ICartRepository` → `SupabaseCartRepository` (registered as Scoped)

**Supabase advisor warnings (post-migration):**
- {Report any new warnings from `get_advisors`. If none, write "None."}

**Build status:** ✅ `dotnet build TheShop.Infrastructure` succeeded with 0 warnings / 0 errors.

**Open questions / TODOs:**
- {If the plan was missing RLS policies, an index, or a constraint, list it here. If none, write "None."}
```

---
