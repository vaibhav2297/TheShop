# Product Catalogue

## 1. Problem Statement

A shopper arriving at The Shop needs one place to see what's for sale and quickly find something worth buying. Without a browsable catalogue, there is no path from "I'm interested" to "this is the product I want" — customers can't compare options, spot a deal, or start a purchase. For a premium store, a cluttered or hard-to-navigate product list also erodes trust before the first click. The catalogue is the front door of the shopping experience, and today it doesn't exist.

**Solution (one line):** A browsable, filterable, sortable, paginated grid of product cards — each showing the product's image, name, and price (with any discount) and presenting Add-to-Cart and Wishlist buttons — that lets any visitor explore the range.

**In scope:**
- A paginated product grid with filtering and sort-by controls.
- A product card showing the product image, name, price (sale price with struck-through original price when discounted), an Add-to-Cart button, and a Wishlist button.
- The card **displays** the Add-to-Cart and Wishlist buttons; their actions are out of scope for this feature (see below).
- English and French for all catalogue text.

**Out of scope:**
- The Add-to-Cart and Wishlist button **actions**, the Cart and Wishlist pages, and full cart/wishlist management — this feature only displays the buttons on the card; adding to the cart, saving to the wishlist, and any sign-in requirement are delivered by separate dedicated features.
- The individual product detail page (a separate feature) — the card links to it.
- Product search, and any admin creation/editing of the products themselves.

> **Detail-page sequencing:** The single-product detail page is a separate feature and may not exist yet. FR-11 / AC-9 specify that a card links to that page and take effect once it ships; until then the link target may be a placeholder. This keeps the catalogue's "click a product" behavior part of its definition of done without pulling detail-page work into this feature.

## 2. Functional Requirements

1. **FR-1:** The catalogue page displays the available products as a grid of product cards.
2. **FR-2:** Each product card displays the product's image.
3. **FR-3:** Each product card shows the product's name and price; when a product is discounted, the card shows the current sale price prominently and the original price (MRP) struck through.
4. **FR-4:** Each product card displays an Add-to-Cart button. The button's action (adding the product to the cart) is out of scope for this feature and is delivered by a separate dedicated feature.
5. **FR-5:** Each product card displays a Wishlist button. The button's action (saving to or removing from the wishlist, and any sign-in requirement) is out of scope for this feature and is delivered by a separate dedicated feature.
6. **FR-6:** Customers can filter the catalogue by product attributes to narrow the products shown.
7. **FR-7:** Customers can sort the catalogue by a set of sort options (such as price and name).
8. **FR-8:** The catalogue is paginated, showing a fixed number of products per page with controls to move between pages.
9. **FR-9:** Both guests and signed-in customers can browse the catalogue.
10. **FR-10:** Within this feature, the Add-to-Cart and Wishlist buttons perform no action when activated; their behavior is delivered by their separate dedicated features.
11. **FR-11:** Selecting a product card outside its action buttons takes the customer to that product's detail page.
12. **FR-12:** All catalogue text — labels, buttons, filter and sort option names, and messages — is available in English and French and follows the active site language.

## 3. Functional Behaviors

### Behavior 1: Browse the catalogue
- **User does:** Opens the catalogue page.
- **User sees:** A grid of product cards, each showing the product image, name, and price, with filter, sort, and pagination controls available.

### Behavior 2: The card's action buttons
- **User does:** Views a product card, sees its Add-to-Cart and Wishlist buttons, and activates one of them.
- **User sees:** Both buttons are present and styled on every card. Within this feature the buttons perform no action when activated — their behavior (adding to the cart, saving to the wishlist, any sign-in prompt) is delivered by separate dedicated features.

### Behavior 3: Filter and sort the results
- **User does:** Applies a filter and/or chooses a sort option.
- **User sees:** The grid updates to show only matching products in the chosen order, returning to the first page of results.

### Behavior 4: Move between pages
- **User does:** Uses the pagination controls to go to another page.
- **User sees:** The next set of products, with the active filter and sort still applied.

### Behavior 5: Open a product's detail page
- **User does:** Clicks a product card (anywhere except its Add-to-Cart or Wishlist buttons).
- **User sees:** They are taken to that product's detail page.

## 4. Constraints

- Prices are shown in Canadian dollars (CAD) using the site's currency format, in both languages.
- When a product has no discount, only its single price is shown — no struck-through original price.
- When a product is discounted, the sale price is always the prominent price and the original price (MRP) is the struck-through secondary price; no separate "% off" badge is shown — the struck-through original price alone conveys the discount.
- Every product card reserves a consistent image area so cards stay aligned; a product without its own image shows a standard placeholder image.
- Within this feature the product card displays the Add-to-Cart and Wishlist buttons only; their actions (and any sign-in requirement) are delivered by separate dedicated features.
- The catalogue shows only products meant to be publicly visible (no hidden, draft, or unpublished products).
- The catalogue can be filtered by category, price range, brand, flavour, and nicotine strength.
- The catalogue can be sorted by: Newest (the default), Price: low → high, Price: high → low, Name: A → Z, and Name: Z → A.
- A fixed number of products is shown per page: 12 products per page.
- Applying or changing a filter or sort returns the customer to the first page of results.
- The catalogue and its controls must be operable by keyboard and expose a visible focus indicator; the Add-to-Cart and Wishlist buttons carry accessible labels.

## 5. Edge Cases & Error Handling

- **Edge case:** A product has no image of its own → **User experience:** A standard placeholder image fills the card's image area so the grid stays aligned.
- **Edge case:** A product is out of stock → **User experience:** The card still appears with a clear "Out of stock" indicator; the Add-to-Cart button is hidden while the Wishlist button is still shown.
- **Edge case:** The active filter and sort combination matches no products → **User experience:** An empty-state message is shown ("No products match your filters") with a way to clear the filters and return to the full list.
- **Edge case:** The catalogue has no products at all → **User experience:** A friendly empty-state message is shown instead of a blank grid.
- **Edge case:** A product name is very long → **User experience:** The name is truncated so cards stay aligned and the grid layout is not broken.
- **Edge case:** Products are still loading → **User experience:** Loading placeholders are shown in place of cards until the products appear.

## 6. Acceptance Criteria

- [ ] **AC-1:** The catalogue displays available products as a grid of cards, each showing the product's image, name, and price. Verifies FR-1, FR-2, FR-3.
- [ ] **AC-2:** A discounted product's card shows both the sale price (prominent) and the original price (struck through); a non-discounted product shows only a single price. Verifies FR-3.
- [ ] **AC-3:** Each product card displays an Add-to-Cart button (its action is out of scope — delivered by a separate feature), and any guest or signed-in customer can browse the catalogue. Verifies FR-4, FR-9.
- [ ] **AC-4:** Each product card displays a Wishlist button (its action is out of scope — delivered by a separate feature). Verifies FR-5.
- [ ] **AC-5:** Activating the Add-to-Cart or Wishlist button performs no action within this feature — no error, no navigation, no state change. Verifies FR-10.
- [ ] **AC-6:** Applying a filter narrows the visible products to those matching and returns to the first page. Verifies FR-6.
- [ ] **AC-7:** Choosing a sort option reorders the products accordingly. Verifies FR-7.
- [ ] **AC-8:** The catalogue is paginated; moving between pages shows the next or previous set without losing the active filter and sort. Verifies FR-8.
- [ ] **AC-9:** Selecting a product card outside its buttons navigates to that product's detail page. Verifies FR-11.
- [ ] **AC-10:** When no products match the active filters, an empty-state message is shown with a way to clear the filters.
- [ ] **AC-11:** An out-of-stock product shows an out-of-stock indicator on its card, with the Add-to-Cart button hidden; its Wishlist button is still shown.
- [ ] **AC-12:** A product without its own image shows a standard placeholder in the card's image area. Verifies FR-2.
- [ ] **AC-13:** All catalogue text appears in both English and French, matching the active site language. Verifies FR-12.
- [ ] **AC-14:** The catalogue and its controls are keyboard-operable with a visible focus indicator, and the Add-to-Cart and Wishlist buttons expose accessible labels.

---

## Assumptions & Open Questions

None — all assumptions confirmed.

---
**Status:** Confirmed   ·   **Created:** 2026-07-01   ·   **Clarified:** 2026-07-01

<!-- Status lifecycle: "Draft — N open assumption(s)" → "Confirmed" once /theshop.clarify resolves them all (N = 0). -->
