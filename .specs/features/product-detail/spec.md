# Product Detail — Specification

Status: Draft

## 1. Problem & Outcome

**Problem:** Shoppers can browse product cards, but cannot open a complete product view, inspect its gallery and content, or see the price and image for a chosen variant.

**Intended outcome:** A guest or signed-in shopper can open a published product, review its details and ordered gallery, choose a variant, see the corresponding price and image, and view optional description and specification sections. Add to Bag and Favourite controls are visible, but their actions remain outside this feature.

## 2. Scope

**Included:**

- A public product-detail route linked from catalogue product cards.
- Brand, product name, category context, price, variant options, availability state, ordered images, description, and specifications.
- A gallery whose primary image is shown first and whose other images can be selected.
- Variant selection that updates the displayed price and selects the variant's pinned image when one exists.
- Display-only Add to Bag and Favourite controls.
- Optional, independently collapsible Description and Specifications sections.
- Loading, not-found, missing-image, non-variant, unavailable-variant, localization, accessibility, and responsive states.

**Excluded:**

- Adding an item to a bag, choosing quantity, bag persistence, stock reservation, checkout, or confirmation feedback.
- Adding or removing favourites, favourite persistence, authentication prompts, or wishlist-page changes.
- Product reviews, recommendations, recently viewed products, sharing, delivery estimates, and inventory management.
- Product authoring, schema redesign, or changes to product publication rules.

## 3. Requirements & Behavior

| ID | Situation / user action | Required behavior |
| --- | --- | --- |
| FR-01 | A guest or signed-in shopper opens a published product from the catalogue or a direct link. | The page loads without authentication and displays the product's brand, name, category context, applicable price, variant choices, availability, gallery, and any optional content. |
| FR-02 | The requested product does not exist or is unpublished. | The page shows the localized product-not-found experience and does not expose product data. |
| FR-03 | A product has gallery images. | Images appear in saved position order with the primary image first. The primary image is the initial hero image. Selecting another image makes it the hero image without leaving the page. |
| FR-04 | A product has no gallery image. | The hero area shows the standard product-image placeholder and no empty thumbnail controls. |
| FR-05 | A product has no variants. | No variant selector is shown. The product's own original/sale price and availability are displayed. |
| FR-06 | A product has variants. | The page shows one choice group per saved option type and one choice per saved option value, using their saved order. A complete choice resolves one exact variant. |
| FR-07 | The shopper selects option values that resolve a variant. | The displayed original/sale price and availability update to that variant. Its pinned gallery image becomes the hero image; without a pin, the product primary image becomes the hero image. The gallery remains browsable. |
| FR-08 | The page first loads a variant product. | The first available variant in saved variant order is selected. If every variant is unavailable, the first saved variant is selected and shown unavailable. |
| FR-09 | The selected product or variant has a sale price. | The sale price is prominent and the original price is secondary and struck through. Otherwise only the original price is shown. Prices use the saved currency and active UI culture. |
| FR-10 | The selected variant is unavailable, or all variants are unavailable. | The page exposes a localized unavailable/out-of-stock state. Add to Bag remains visible but disabled. Favourite remains visible. |
| FR-11 | The shopper views or activates Add to Bag or Favourite. | Both controls have localized accessible names. Within this feature they cause no request, navigation, persisted state, or success message. |
| FR-12 | The product has a non-empty description. | A Description section is shown and can be expanded or collapsed. Persisted supported formatting renders as display content and cannot execute active content. |
| FR-13 | The product has one or more specifications. | A Specifications section is shown and can be expanded or collapsed. Rows appear in saved position order with name/value association preserved. |
| FR-14 | Description is empty, or specifications are empty. | The corresponding section is omitted. No empty heading or placeholder section is rendered. |
| FR-15 | The shopper uses keyboard, touch, or assistive technology. | Gallery choices, variant choices, collapsible sections, and buttons are operable; focus is visible; selected/expanded/disabled state and image purpose are exposed accessibly. |
| FR-16 | The active interface language is English or French. | All interface labels, states, image alternative text, and error messages follow the active language. Product-authored content remains unchanged. |

## 4. Constraints & References

**Applicable principles:** ARCH-01, ARCH-03, ARCH-04, ARCH-05, ARCH-07, ARCH-08, ARCH-09, ARCH-10, TEXT-11, DESIGN-14, DESIGN-15, DESIGN-16, DESIGN-18, DESIGN-19, WEB-20, WEB-21, WEB-22, COMP-25, STYLE-26, STYLE-27, STYLE-28, TEST-29, DOC-30.

**Other mandatory constraints:**

- Use the existing `Product` aggregate, ordered gallery, option types/values, variants, pinned image, description, and specification data. Do not duplicate product business rules in Web.
- UI consumes an immutable Application DTO through a MediatR query. It must not consume the Domain entity or call the repository directly.
- The public read path explicitly filters to published products. Existing RLS remains the security boundary and already permits public reads only for published product rows and children.
- Reuse existing currency formatting, placeholder imagery, breadcrumbs, localized resources, MudBlazor primitives, and centralized route/busy-state conventions.
- No database migration or dependency is expected. Any discovered schema or policy change requires a spec revision before implementation.
- `.specs/principles.md` currently says `Status: Proposed for adoption`. The rule text is treated as mandatory for this draft, but implementation verification cannot resolve the adoption note without the owner decision in Q-02.

**Design references — UI only:**

| UI / state | Exact Figma frame link | Viewport | Reference version/date, if available |
| --- | --- | --- | --- |
| Product detail | https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop?node-id=2263-5259&t=pEThVj7zVkr4qcQ4-11 | Pending Figma MCP access | Attempted 2026-09-19; Figma API returned 429 with no frame data |

**Agreed visual deviations — if any:** None. Exact layout, responsive breakpoints, component states, and initial accordion state remain unverified until the supplied frame is retrievable.

## 5. Acceptance Criteria

- [ ] AC-01: A guest or signed-in shopper can open a published product from its catalogue card and see its brand, name, category context, current price, availability, gallery, and variant choices where applicable. Covers FR-01 and FR-05–FR-09.
- [ ] AC-02: An unknown or unpublished product produces the localized not-found experience and exposes no product details. Covers FR-02.
- [ ] AC-03: Gallery images render with the primary image first; selecting another thumbnail changes the hero image; a product with no images shows the standard placeholder without empty thumbnails. Covers FR-03 and FR-04.
- [ ] AC-04: A variant product initially selects the first available saved variant. Changing option values updates price, sale-price treatment, availability, and the hero image to the resolved variant's pin or the primary-image fallback. Covers FR-06–FR-10.
- [ ] AC-05: Add to Bag and Favourite are visible and accessible. Activating either causes no request, navigation, persisted state, or success feedback; Add to Bag is disabled for an unavailable selection. Covers FR-10 and FR-11.
- [ ] AC-06: Description and Specifications appear only when their corresponding content exists; each visible section expands and collapses independently; specification order and supported description formatting are preserved safely. Covers FR-12–FR-14.
- [ ] AC-07: Gallery, variant, accordion, and action controls work with keyboard and assistive technology, expose visible focus and state, and all interface text follows English/French selection while authored content remains unchanged. Covers FR-15 and FR-16.
- [ ] AC-08: The implemented loading, populated, unavailable, optional-content, and responsive states match the supplied Figma frame at its verified viewport(s), subject only to recorded deviations.

## 6. Assumptions & Open Questions

**Product assumptions:**

- Product identity uses the existing stable `Guid`, yielding `/products/{id:guid}`. The current model has no product slug, and stable IDs preserve links after renaming.
- A variant pin changes the hero selection but does not reorder or filter the gallery.
- The first available saved variant is the least surprising initial selection and avoids defaulting Add to Bag to a disabled state when a sellable choice exists.
- Product-authored description and specification text is not translated when interface language changes.

| ID | Product question | Blocks what? | Decision / owner |
| --- | --- | --- | --- |
| Q-01 | Should Description and Specifications start expanded or collapsed at each responsive viewport? | Exact UI behavior and visual AC-08; implementation can proceed after Figma access or owner direction. | Pending because Figma MCP returned 429 on 2026-09-19. |
| Q-02 | Should `.specs/principles.md` now be considered adopted despite its `Proposed for adoption` header and adoption clarifications? | Claiming principle compliance and advancing beyond Draft under the SDD workflow. | Pending owner confirmation. |

