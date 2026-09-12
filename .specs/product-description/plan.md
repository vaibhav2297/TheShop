# Implementation Plan — Product Description

> Companion to `.specs/product-description/spec.md`. This plan is technical (HOW); the spec is
> non-technical (WHAT/WHY). Read the spec first.

## 1. Objective

Replace the product form's plain-text description with a rich-text description bounded at 20,000
text characters, and add an ordered set of name/value specification rows to the `Product`
aggregate. Description markup is constrained to one whitelist grammar enforced in three places:
the Quill editor (authoring), the `ProductDescription` domain value object (write path), and a
Postgres `CHECK` on `products.description` (durable boundary). Specifications become a fourth
aggregate child alongside images, option types, and variants, persisted through the existing
`save_product` RPC in the same transaction.

## 2. Tech Stack

- **Domain:** C#, no external dependencies. Markup grammar and text-length counting are BCL-only
  (`Regex`, `string`).
- **Application:** MediatR, FluentValidation, `Result<T>` (project-internal), hand-written mappers.
- **Infrastructure:** `supabase-csharp` 1.1.1 through the existing `save_product` RPC.
- **Web:** MudBlazor 9.7.0, Quill 2.0.3 vendored into `wwwroot/lib/quill/` and driven through a
  project-owned JS interop module (Decision 1). No new NuGet package. bUnit for component tests.
- **Persistence:** Supabase (PostgreSQL + RLS).

## 3. High-level Architecture

Description and specifications ride the existing create/update product path. No new command, no
new repository interface, no new HTTP call. The aggregate grows one value object and one child
collection; the RPC payload grows one array.

```
Staff edits <ShopRichTextEditor> / specification rows in ProductContentCard.razor
   ↓
ProductForm.SaveAsync -> IMediator.Send(CreateProductCommand | UpdateProductCommand)
   ↓
ValidationBehavior -> Create/UpdateProductCommandValidator   // grammar, 20,000 chars, row rules
   ↓
Create/UpdateProductHandler (Application)
   ├── product.UpdateDetails(name, ProductDescription.Create(html), category, brand)
   └── product.SetSpecifications(IReadOnlyList<ProductSpecificationInput>)   // RULE-3, RULE-4
   ↓
SupabaseProductRepository.AddAsync / UpdateAsync -> save_product(payload) RPC
   ↓
products.description (CHECK grammar + size) · product_specifications (RLS-gated)
   ↓
Result<AdminProductDto> -> ProductForm -> Snackbar + navigate, or per-field/per-row error
```

## 4. Data Model

### Domain entities & value objects

- **`ProductDescription`** (new value object) — `Html`, `PlainText`, `TextLength`, `IsEmpty`,
  `MaxTextLength = 20_000`, `MaxHtmlBytes = 200_000`, `Empty`.
  `Create(string? html)` enforces the Section 5 Decision 3 grammar and the 20,000 text-character
  bound. `Rehydrate(string html)` skips both, so persisted rows always load.
  Throws `ProductDescriptionUnsupportedContentException`, `ProductDescriptionTooLongException`.
- **`ProductSpecification`** (new entity, aggregate child) — `Id`, `Name`, `Value`, `Position`.
  `Create(Guid? id, string name, string value, int position)` trims and rejects blank name or
  value. Throws `SpecificationNameRequiredException`, `SpecificationValueRequiredException`.
- **`ProductSpecificationInput`** (new value object) — `Id`, `Name`, `Value`. Input shape for
  `Product.SetSpecifications`, mirroring `ProductOptionTypeInput`.
- **`Product`** (modified) — `Description` changes type `string` → `ProductDescription`.
  New `IReadOnlyList<ProductSpecification> Specifications`. New
  `SetSpecifications(IReadOnlyList<ProductSpecificationInput>)` replacing the set wholesale and
  enforcing RULE-4 name uniqueness after `Trim().ToLowerInvariant()`. Throws
  `DuplicateSpecificationNameException`. `MaxDescriptionLength` constant and `ValidateDescription`
  are removed; the value object owns that invariant.

### DTOs (Application → Web)

- **`ProductSpecificationDto`** (new) — `Guid Id`, `string Name`, `string Value`, `int Position`.
- **`SpecificationInput`** (new) — `Guid? Id`, `string Name`, `string Value`, `int Position`.
- **`AdminProductDto`** (modified) — new field
  `IReadOnlyList<ProductSpecificationDto> Specifications`. `Description` stays `string?` and now
  carries HTML.
- **`CreateProductCommand` / `UpdateProductCommand`** (modified) — new field
  `IReadOnlyList<SpecificationInput> Specifications`. `Description` stays `string?` (HTML).

### Database tables (new or modified)

| Table | Purpose | Key columns |
|---|---|---|
| `product_specifications` (new) | Ordered name/value rows owned by one product | `id`, `product_id`, `name`, `value`, `position` |
| `products` (modified) | Description becomes bounded HTML | `description` gains two `CHECK` constraints; existing plain text migrated to escaped HTML |

### Indexes

- `ux_product_specifications_name (product_id, lower(btrim(name)))` — enforces RULE-4 in the
  database.
- `idx_product_specifications_product (product_id, position)` — serves the edit-form load and the
  ordered read.

## 5. Core Design Decisions

1. **Decision:** Drive Quill 2.0.3 directly through a project-owned component,
   `Components/Common/ShopRichTextEditor.razor` + `.razor.cs`, backed by
   `wwwroot/js/shop-rich-text-editor.js`. Quill is vendored into `wwwroot/lib/quill/`
   (`quill.min.js`, `quill.snow.css`), pinned by version, and the JS module injects its `<link>` and
   `<script>` on first initialization so pages that never open the editor never download it.
   - **Why:** The toolbar is custom regardless — Quill's own toolbar ships Quill SVG icons and
     Rule 19 requires `ShopIcons` — so a wrapper package would only supply value binding, a text
     getter, and asset loading, which is roughly 150 lines of interop. Against that, the direct
     route buys three things. **Payload:** `MudExRichTextEditor` 9.4.0 requires
     `MudBlazor.Extensions` 9.4.0, whose nuspec lists 19 direct dependencies — `SixLabors.ImageSharp`,
     `ExcelDataReader`, `SharpCompress`, `MsgReader`, `RtfPipe`, `Gotho.BlazorPdf`, `AuralizeBlazor`,
     `MudBlazor.Markdown`, `MetadataExtractor`, `Nextended.Core`/`.Blazor`, `OneOf`, `BlazorJS` —
     plus their own, every one of which a Blazor **WebAssembly** browser downloads; these are large,
     reflection-heavy assemblies that IL trimming handles poorly. Direct Quill adds zero managed
     assemblies and the same Quill JS the wrapper ships. **Grammar control:** Decision 3's whitelist
     is enforced by a Postgres `CHECK`, so it only holds if the emitted HTML is predictable; owning
     the Quill `formats` array and pinning the Quill version makes that a configuration rather than
     a hope. **Version independence:** `MudBlazor.Extensions` 9.4.0 pins MudBlazor 9.4.0 while this
     project is on 9.7.0, tying future MudBlazor upgrades to a third party's cadence.
   - **Cost accepted:** the project owns the interop lifecycle — `IJSObjectReference`,
     `DotNetObjectReference` callback, `IAsyncDisposable`, and a link-insertion dialog Quill's
     `snow` toolbar would otherwise have provided.
   - **Rejected:** `MudExRichTextEditor` 9.4.0 — the payload and coupling above, for a wrapper whose
     main surface (the toolbar) this feature replaces anyway. Rejected: `MudTextField` with `Lines`
     — cannot satisfy FR-1 formatting. Rejected: loading Quill from a CDN — the editor must work
     without third-party network access, and a pinned local copy is what makes the Decision 3
     grammar reproducible.
   - **Rule 14 note:** `ShopRichTextEditor` is a custom UI primitive. Rule 14 permits one only after
     explicit user confirmation; that confirmation was given when the user chose this route over the
     wrapper package. `ShopRichTextEditor` inherits `MudComponentBase` and forwards `Class` / `Style`
     to its root element per Rules 23 and 24; its theme overrides live in
     `Styles/components/_rich-text-editor.scss` per Rule 28, never in a `wwwroot` page stylesheet.

2. **Decision (superseded — see waiver below):** Register exactly the FR-1 formats on the Quill
   instance — `formats: ['header', 'bold', 'italic', 'list', 'link']` — with
   `modules: { toolbar: [...] }` set to Quill's own `snow` toolbar, restricted to those same
   formats. No underline, strike, color, background, align, indent, image, video, or table. Read
   content back with `quill.getSemanticHTML()`, never `quill.root.innerHTML`.
   **Heading levels: all six.** `header` accepts 1–6 and the Decision 3 grammar already admits
   `<h1>`–`<h6>`; Quill's native toolbar renders this as one `header: [1,2,3,4,5,6,false]` dropdown
   control.
   - **Why:** Quill drops unregistered formats when content is pasted or inserted, which is FR-7's
     "unsupported formatting is removed" behavior delivered by the editor rather than by a
     hand-written scrubber. A narrow registered set is also what keeps the Decision 3 grammar small
     enough to enforce in SQL. `root.innerHTML` carries Quill's internal markers — `class="ql-*"`
     and `<ol data-list="bullet">` for bullet lists — which the grammar rejects; `getSemanticHTML()`
     is the documented API that normalizes them.
   - **Waiver (explicit, user-directed):** originally this decision drove formatting from
     MudBlazor `MudIconButton`s + a `MudMenu` heading picker, calling `quill.format(...)`, to avoid
     Quill's own toolbar — because that toolbar violates Rule 19 (`ShopIcons` only, Quill ships its
     own SVG icons), Rule 2 (MudBlazor components only, the toolbar is raw Quill DOM), and Rule 3
     (no hardcoded strings, Quill's toolbar `title` attributes are English-only, not resx-driven).
     The user explicitly waived Rules 2, 3, and 19 for this one component, to use Quill's built-in
     `snow` toolbar directly instead of hand-building an equivalent. `ShopRichTextEditor` mounts
     Quill on an inner div it appends to its host, so the toolbar's DOM insertion (a `.ql-toolbar`
     sibling) never fights Blazor's diff of a Razor-authored region. `ShopLinkDialog` and the
     `ProductDescription_*` toolbar-label resx keys were removed as dead code — Quill's own toolbar
     supplies its link-entry UI and (unlocalized) control labels now.

3. **Decision:** One canonical allowed-markup grammar, restated verbatim in three enforcement
   points (`ProductDescription`, the FluentValidation validators, the Postgres `CHECK`).
   Allowed tokens, matched case-insensitively, nothing else:
   - `<p>`, `</p>`, `<br>`, `<br/>`, `<br />`
   - `<h1>`…`<h6>`, `</h1>`…`</h6>`
   - `<strong>`, `</strong>`, `<em>`, `</em>`
   - `<ol>`, `</ol>`, `<ul>`, `</ul>`, `<li>`, `</li>`
   - `<a href="URL">` and `</a>`, where `URL` begins `https://`, `http://`, or `mailto:` and
     contains no `"`, `<`, or `>`; optional `target="_blank"` and `rel="noopener noreferrer"`
     attributes are permitted in either order.

   Any other tag token, any other attribute, and any `javascript:` or `data:` href fails the
   check. Enforcement **rejects**; it never rewrites. Rejection is safer than regex-based
   sanitizing, and matches RULE-6's "no partial save".
   - **Why:** RULE-5 and AC-11 require that supplied code never executes. Rejecting anything
     outside a closed grammar is verifiable and testable; a rewriting sanitizer is not.
   - **Rejected:** `Ganss.Xss` + `AngleSharp` behind an Application interface — roughly 2 MB of
     managed assemblies added to a Blazor **WebAssembly** payload to run a sanitizer that a
     `products.edit` holder can bypass by calling the RPC directly. It buys no trust boundary the
     `CHECK` does not already give, at real download cost.

4. **Decision:** The Postgres `CHECK` on `products.description` is the security boundary; the C#
   grammar check is fast feedback.
   - **Why:** Blazor WebAssembly runs Domain, Application, and Infrastructure in the browser, so
     no C# check is a trust boundary (constitution `architecture-admin.md` — RLS and database
     constraints are the only real boundary). Every write path — the `save_product` RPC and the
     direct Postgrest `products` update policy from `0023_products_admin_writes` — passes through
     the table constraint.
   - **Rejected:** Trusting write-time C# sanitization alone. Rejected: a plpgsql sanitizer that
     rewrites content — unreasonable to write and to keep correct.

5. **Decision:** `TextLength` = strip every tag, mapping `</p>`, `</h1>`–`</h6>`, `</li>`, and
   `<br>` to one `\n` each; decode `&amp;`, `&lt;`, `&gt;`, `&quot;`, `&#39;`, `&nbsp;` to one
   character each; `Trim()` the result; take `.Length`.
   - **Why:** FR-3 counts spaces and each paragraph or line break once, and excludes formatting and
     link destinations. Trimming makes an editor-empty document (`<p><br></p>`) count 0, which is
     what AC-6 needs. AC-4 and AC-5 pin the boundary at exactly 20,000 and 20,001 under this rule.
   - **Rejected:** Counting raw HTML length — link destinations and tags would consume the
     allowance, contradicting FR-3.

6. **Decision:** Bound raw HTML at 200,000 bytes (`octet_length`) alongside the 20,000 text-character
   bound.
   - **Why:** Text length alone does not bound the payload; deeply nested markup could inflate one
     row without limit. Ten times the text bound leaves headroom for normal formatting.
   - **Rejected:** No raw bound — leaves an unbounded row and an unbounded RPC payload.

7. **Decision:** Migrate existing `products.description` values from plain text to escaped HTML
   paragraphs in the same migration that adds the `CHECK`.
   - **Why:** AC-3 requires an existing description containing literal angle brackets to survive
     open-and-save intact. If a legacy plain-text row reaches Quill unescaped, `<` starts a tag and
     the text is lost. Escaping once at rest is the only place the fix holds for every read path.
   - **Rejected:** Escaping on read in the mapper — every read path would have to guess whether a
     row is legacy text or HTML, forever.

8. **Decision:** `ProductDescription.Rehydrate` bypasses grammar and length checks;
   `ProductDescription.Create` applies both.
   - **Why:** Mirrors `Product.Rehydrate`. A stored row must always load, even if a bound changes
     later; only a staff write is judged.
   - **Rejected:** Validating on load — an existing product would become unopenable after any
     tightening of the grammar.

9. **Decision:** Specifications are an aggregate child replaced wholesale by
   `Product.SetSpecifications`, persisted by the existing `save_product` RPC's
   delete-before-insert sequence.
   - **Why:** Identical lifecycle to `product_option_types` and `product_images`. Reusing the one
     transactional RPC keeps RULE-6 ("no partial save") true for free and adds no second write path.
   - **Rejected:** A separate `SetProductSpecificationsCommand` — would let specifications save
     while the rest of the product save fails, breaking RULE-6.

10. **Decision:** Specification rows cannot be rearranged. A row's position is the order it was
    added in; `Position` is assigned by index and renumbered only when a row is removed.
    - **Why:** The clarified spec puts rearranging out of scope (FR-4, "Staff cannot rearrange
      existing rows"), so no reorder control is built. This also matches Figma `2816:6685`, which
      draws exactly one icon button per row.
    - **Still required:** order *preservation*. Rows reopen in the order they were added (FR-4,
      AC-1, AC-2), which the `position` column and the ordered read already deliver — nothing about
      the data model changes.
    - **Rejected:** up/down buttons or `MudDropContainer` — both build a capability the spec
      excludes.

11. **Decision:** After removing a row, move focus to the next row's name field, or to the "Add
    specification" button when the removed row was last.
    - **Why:** Spec Section 5's last edge case, and AC-13's "row removal retains usable focus".
    - **Rejected:** Leaving focus on the detached button — focus falls to `<body>`.

12. **Decision:** Keep `Description` typed `string?` on the commands and on `AdminProductDto`;
    only the Domain uses `ProductDescription`.
    - **Why:** Constitution Rule 7 — entities and their value objects do not cross to the Web
      layer. The editor binds a plain HTML string.
    - **Rejected:** Exposing `ProductDescription` on the DTO — leaks a Domain type into Web.

13. **Decision:** Description and specifications live together in one new "CONTENT" card
    (`ProductContentCard`), placed between General Info and Variants. The description field is
    removed from General Info.
    - **Why:** Figma `2816:6685` draws exactly this — a card headed "CONTENT" holding a Description
      section above a Specification section — and `2816:6387` has no description field left in it.
      One component per designed card keeps the mapping checkable at review, and matches how
      `ProductVariantsCard` already owns the Variants card.
    - **Departure — required marker:** the design marks Description with a required `*`. The spec
      makes description optional (FR-3, RULE-2, AC-6), and the same card marks Sale Price required
      when it is not, so the asterisks are unreliable. Follow the spec: no `Required`, no
      `RequiredError`.
    - **Rejected:** a separate specifications card below a separate description card — two
      components where the design has one, and a heading the design does not contain.

## 6. Core Functional Flow

### Flow 1: Write description (spec Behavior 1)

1. `ProductContentCard.razor`, mounted by `ProductForm.razor`, renders
   `<ShopRichTextEditor Value="@Description" ValueChanged="OnDescriptionChanged" />` under a sibling
   `MudText Typo="Typo.caption"` label (`Strings.AddProduct_DescriptionLabel`), referenced by
   `aria-labelledby`. The field is optional — no `Required`, per Decision 13.
2. Quill's `text-change` event calls back into `ShopRichTextEditor` through a
   `DotNetObjectReference`, carrying `getSemanticHTML()` and `getText().length`. The component
   raises `ValueChanged`; the card bubbles it to `ProductForm.OnDescriptionChanged(string? html)`,
   which sets `_dirty = true` and stores the HTML. The card renders the live counter from the
   reported text length.
3. Save dispatches `CreateProductCommand` / `UpdateProductCommand` with `Description = html`.
4. `ValidationBehavior` runs the validator: grammar → `Product_DescriptionUnsupportedContent`;
   text length > 20,000 → `Product_DescriptionTooLong`; `octet_length` > 200,000 →
   `Product_DescriptionTooLarge`.
5. Handler calls `Product.Create(...)` / `product.UpdateDetails(...)` with
   `ProductDescription.Create(request.Description)`. A `DomainException` becomes
   `Result.Fail(ex.MessageKey)`.
6. `SupabaseProductRepository` sends `description` in the `save_product` payload; the table `CHECK`
   is the final gate.
7. Success → `Snackbar` shows `Strings.Product_Created` / `Product_Updated` and navigates.
   Failure → `Snackbar` shows `Localizer[result.Error]`; `_description` is untouched, so the
   staff member's content stays editable (RULE-6, AC-5).

### Flow 2: Manage specifications (spec Behavior 2)

1. `ProductContentCard` raises `StateChanged(ProductSpecificationsState)` on add, edit, and
   remove; `ProductForm.OnSpecificationsStateChanged` stores rows and sets `_dirty`.
2. Rows carry `Position` equal to their index. A new row appends at the end; removing a row
   renumbers the rows after it. Nothing else changes a row's position (Decision 10).
3. Save sends `Specifications` in command order.
4. Validator rejects a blank name or value (`Product_SpecificationNameRequired`,
   `Product_SpecificationValueRequired`) and duplicate normalized names
   (`Product_SpecificationNameDuplicated`), each carrying the offending row positions in
   `ErrorArgs`.
5. `product.SetSpecifications(...)` re-asserts the same invariants; `DuplicateSpecificationNameException`
   becomes `Result.Fail(ex.MessageKey)`.
6. `save_product` deletes the product's specification rows and re-inserts the submitted set inside
   the existing transaction.
7. `AdminProductDtoMapper.ToAdminDto` returns rows ordered by `Position`, so reopening shows the
   saved order (AC-1, AC-2).

### Flow 3: Correct rejected content (spec Behavior 3)

1. Any validator or domain failure returns `Result.Fail(key, args)` before the repository is
   called — nothing is written, so the saved product is unchanged (RULE-6, AC-5, AC-7, AC-8).
2. `ProductForm` keeps `_description` and the specification rows in component state; no reload
   happens on failure.
3. `ProductContentCard` reads the returned row positions from `Result.ErrorArgs` and marks
   those rows, mirroring `ProductForm.FlagVariantsReportedByServer`.
4. Client-side `MudTextField` validation on each row's name and value shows the same messages
   before a round trip; duplicate names are flagged by comparing normalized names in the component.

## 7. Development Plan

### Step 1 — Domain (`shop-domain-implementer`)

**Depends on:** resolved plan.

- [ ] **TASK-001** — Create `ProductDescription` in `TheShop.Domain/ValueObjects/`. Implement the
      Decision 3 grammar and the Decision 5 counting rule. Expose `Html`, `PlainText`,
      `TextLength`, `IsEmpty`, `MaxTextLength = 20_000`, `MaxHtmlBytes = 200_000`, `Empty`,
      `Create(string? html)`, `Rehydrate(string html)`. `Create(null)` and `Create("")` return
      `Empty`; content whose trimmed text length is 0 normalizes to `Empty` (AC-6).
- [ ] **TASK-002** — Create `ProductSpecification` in `TheShop.Domain/Entities/` and
      `ProductSpecificationInput` in `TheShop.Domain/ValueObjects/`, following
      `ProductOptionType` / `ProductOptionTypeInput` verbatim in shape.
- [ ] **TASK-003** — Create `ProductDescriptionUnsupportedContentException`,
      `SpecificationNameRequiredException`, `SpecificationValueRequiredException`, and
      `DuplicateSpecificationNameException` in `TheShop.Domain/Exceptions/`, each with a
      `MessageResourceKey` constant per Section 9. Update
      `ProductDescriptionTooLongException`'s XML summary from 2000 to 20,000 characters.
- [ ] **TASK-004** — Modify `Product`: `Description` becomes `ProductDescription`; delete
      `MaxDescriptionLength` and `ValidateDescription`; `Create` and `UpdateDetails` take
      `ProductDescription`; `Rehydrate` takes `ProductDescription` and an optional
      `IReadOnlyList<ProductSpecification>? specifications`; add
      `IReadOnlyList<ProductSpecification> Specifications` and
      `SetSpecifications(IReadOnlyList<ProductSpecificationInput>)` enforcing RULE-3 and RULE-4.
      Update every Domain call site.
- [ ] **TASK-005** — Domain unit tests for `ProductDescription` grammar and counting boundaries and
      for `SetSpecifications` invariants, owned by `shop-test-writer` during implementation test
      verification. Domain implementer supplies literal API only.

**Completion gate:** Domain builds · no outer-layer type leaked into Domain · public API reported
for Application. Test specialist covers invariants before overall Implement completion.

### Step 2 — Application (`shop-application-implementer`)

**Depends on:** Step 1's reported Domain API.

- [ ] **TASK-006** — Add `ProductSpecificationDto` and `SpecificationInput` to
      `Features/Products/DTOs/`. Add
      `IReadOnlyList<ProductSpecificationDto> Specifications` to `AdminProductDto`.
- [ ] **TASK-007** — Add `IReadOnlyList<SpecificationInput> Specifications` to
      `CreateProductCommand` and `UpdateProductCommand`. In both handlers, build the description
      through `ProductDescription.Create(request.Description)` and call
      `product.SetSpecifications(ProductInputMapper.ToSpecificationInputs(request.Specifications))`
      before the publish check. Existing `DomainException` catch blocks already translate the new
      exceptions.
- [ ] **TASK-008** — Extend `CreateProductCommandValidator` and `UpdateProductCommandValidator`:
      replace the 2,000-character description rule with the Section 9 description rules, and add
      the specification row rules. Carry offending row positions in the failure so
      `Result.ErrorArgs` reaches the form.
- [ ] **TASK-009** — Add the Section 9 keys to `ProductErrorKeys`, and the matching entries to
      `TheShop.Web/Resources/Strings.resx` and `Strings.fr.resx`. Update
      `Product_DescriptionTooLong` in both files from 2000 to 20,000. Extend
      `AdminProductDtoMapper.ToAdminDto` with `Specifications` ordered by `Position` and map
      `product.Description.Html`; extend `ProductInputMapper` with `ToSpecificationInputs`.
      `IProductRepository` is unchanged — confirm and report that.
- [ ] **TASK-010** — Application unit tests for both validators and both handlers, owned by
      `shop-test-writer` before Implement completion.

**Completion gate:** command folders keep the feature-folder convention · handlers translate every
Section 9 outcome · all contracts consumed by Infrastructure and Web are written and compiling.

### Step 3 — Contract freeze

| Contract | Owner | Consumers | Status |
|---|---|---|---|
| `ProductDescription` public API | Domain | Application | Stable |
| `Product.SetSpecifications` / `Product.Specifications` | Domain | Application | Stable |
| `CreateProductCommand` / `UpdateProductCommand` with `Specifications` | Application | Web | Stable |
| `AdminProductDto` with `Specifications` | Application | Web | Stable |
| `ProductSpecificationDto` / `SpecificationInput` shape | Application | Web | Stable |
| `ProductErrorKeys` new keys + `ErrorArgs` row-position tokens | Application | Web | Stable |
| `IProductRepository` (unchanged) | Application | Infrastructure | Stable |

### Step 4 — Infrastructure (`shop-infra-implementer`) — runs in parallel with Step 5

**Depends on:** contract freeze (Domain API and `ProductErrorKeys` stable).

- [ ] **TASK-011** — Migration `0029_product_specifications.sql`, applied via Supabase MCP: create
      `product_specifications` with its two indexes and its four RLS policies. Exact SQL is
      Section 10 — apply it verbatim. Not blocked by anything in Step 5.
- [ ] **TASK-012** — Replace `public.save_product(jsonb)` with a version that, on update, adds
      `DELETE FROM product_specifications WHERE product_id = v_product_id;` to the existing
      delete-before-insert block, and in both modes inserts every element of
      `payload->'specifications'`. Change nothing else in the function.
- [ ] **TASK-013** — Add `ProductSpecificationRecord` to `Persistence/Records/` and its
      `ToDomain()` extension to `Persistence/Mappers/`. Load specifications in
      `SupabaseProductRepository.GetForEditAsync` and pass them to `Product.Rehydrate`; emit a
      `specifications` array from `BuildSavePayload`. Update `ProductMapper.ToDomain` to build the
      description through `ProductDescription.Rehydrate`.
- [ ] **TASK-014** — Translate the new database failures into the Section 9 keys: the
      `products_description_markup_allowed` violation → `Product_DescriptionUnsupportedContent`,
      `products_description_size` → `Product_DescriptionTooLarge`, and
      `ux_product_specifications_name` → `Product_SpecificationNameDuplicated`, following the
      existing `IsSkuUniqueViolation` pattern.
- [ ] **TASK-023** — Migration `0030_product_description_bounds.sql`, applied via Supabase MCP:
      escape legacy plain-text descriptions to HTML, then add `products_description_size` and
      `products_description_markup_allowed`. Exact SQL is Section 10 — apply it verbatim, in that
      order. **Blocked** until TASK-016 reports its `getSemanticHTML()` capture and Decision 3's
      token list is frozen against it; applying the `CHECK` against an unverified grammar would
      reject every save. This is the one cross-step dependency between Step 4 and Step 5;
      TASK-011, TASK-012, TASK-013, and TASK-014 do not wait on it.

**Completion gate:** migrations apply cleanly · RLS policies match Section 10 verbatim ·
TASK-023 applied only after the TASK-016 capture is reported ·
`save_product` still writes product, gallery, option types, variants, and SKU registry in one
transaction · no Infrastructure type leaks inward.

### Step 5 — Web (`shop-ui-implementer`) — runs in parallel with Step 4

**Depends on:** contract freeze (command and DTO shapes stable).

**Figma references** *(re-fetched by `shop-ui-implementer` at implementation time)*

- **File:** `https://www.figma.com/design/63Ieb8AduwMHoVHwzZ7UO3/The-Vape-Shop`
- **Nodes:**
  - `2816:6378` — "Create Products - Content", the whole admin create/edit page frame this feature
    changes; supplied by the user through `--figma`.
  - `2816:6387` — "General Info" card (y 4447, height 607). Its fields are Add Images, Name, Price,
    Sale Price, Brand, Category, Status. It carries **no** description field: the description moves
    out of this card into the new one below.
  - `2816:6685` — the card this feature adds (y 5054, height 463), stacked between General Info and
    Variants. Its layer name is `Contetn` (a typo in the file); its rendered heading is **"CONTENT"**.
    It holds two sections:
    - `…;2816:6721` "Description" (height 165) — a "Labeled Text Field" instance: a caption label
      reading "Description" plus a multiline text field. This is the node the rich-text editor
      replaces; no formatting toolbar is drawn in the design, so the toolbar in TASK-016 is new
      work with no Figma counterpart.
    - `…;2816:6882` "Specififcation" (height 145) — a "Specification" caption, one row consisting
      of a 448 px name field (sample value "Material") and an 896 px value field followed by a
      single 48 px icon button, then an "Add Specification" button carrying `Edit / Add_Plus`.
  - `2816:6388` — "Variants" card, unchanged; confirms the new card sits above it.

  **One deliberate departure from this design** (Decision 13): the design marks Description with a
  required `*`, and the spec makes description optional (FR-3, RULE-2, AC-6) — follow the spec. The
  single icon button per specification row is correct as drawn: it is remove, and the clarified
  spec puts rearranging out of scope (Decision 10).

- [ ] **TASK-015** — Vendor Quill 2.0.3 (`quill.min.js`, `quill.snow.css`) into
      `wwwroot/lib/quill/` and build `Components/Common/ShopRichTextEditor.razor` + `.razor.cs` +
      `wwwroot/js/shop-rich-text-editor.js`. The component inherits `MudComponentBase`, forwards
      `Class` / `Style` to its root via `CssBuilder` / `StyleBuilder` (Rules 23, 24), and exposes
      `Value`, `ValueChanged`, `Disabled`, `Placeholder`, `AriaLabelledBy`, and `TextLength`. The JS
      module injects Quill's `<link>` and `<script>` once (cached promise) and exposes
      `init(host, dotNetRef, options)`, `setHtml(html)`, and `dispose()`. Initialize with
      `formats: ['header','bold','italic','list','link']` and Quill's own `snow` toolbar,
      restricted to those formats, per the Decision 2 waiver. Load stored content through
      `quill.clipboard.dangerouslyPasteHTML(html)` so it is parsed into a Delta against the
      registered formats rather than assigned to `innerHTML` — this is the path AC-11 depends on;
      the host `<div @ref>` renders with no child content so Blazor never manages Quill's DOM.
      Implement `IAsyncDisposable`, disposing the `DotNetObjectReference` and the
      `IJSObjectReference`. Theme overrides go in `Styles/components/_rich-text-editor.scss`
      (Rule 28).
- [ ] **TASK-016** — Delete the description `MudTextField` from the General Info panel in
      `ProductForm.razor` (Figma `2816:6387` no longer carries it) and render `<ShopRichTextEditor>`
      inside the new Content card instead, bound to the card's `Description` parameter, with a
      sibling `MudText Typo="Typo.caption"` label. Quill's own `snow` toolbar is the formatting UI
      (Decision 2 waiver) — no MudBlazor toolbar buttons, `MudMenu` heading picker, or link dialog
      to build; the toolbar's native `header`/`bold`/`italic`/`list`/`link` controls call
      `quill.format(...)` and track their own active state internally. Add a live `{n} / 20000`
      counter fed by the component's `TextLength`, and a
      `Severity.Info` `MudAlert` raised from a `paste` listener that inspects
      `clipboardData.getData('text/html')` for tags or attributes outside the grammar (FR-7, AC-10).
      Capture Quill's actual `getSemanticHTML()` output for each of the seven supported formats at
      2.0.3 and confirm it matches the Decision 3 grammar; report any token the grammar must gain.
      **This capture gates TASK-023** — report it before the description `CHECK` is applied, and
      amend Decision 3, `ProductDescription`, and both validators together if a token is missing.
- [ ] **TASK-017** — Create `Components/Products/ProductContentCard.razor` + `.razor.cs` +
      `ProductSpecificationsState.cs`, modelled on `ProductVariantsCard` and mapping 1:1 to Figma
      `2816:6685`: one `MudExpansionPanels`/`MudExpansionPanel` headed `Strings.ProductContent_Heading`
      ("CONTENT"), holding the description section from TASK-016 above a specification section. Each
      specification row is a name `MudTextField` (Figma 448 px), a value `MudTextField` (896 px),
      and a single remove `MudIconButton` — no reorder control, per Decision 10 — followed by an "Add Specification"
      `MudButton` with `ShopIcons.Outlined.Add_Plus`. Parameters: `Description`,
      `DescriptionChanged`, `InitialSpecifications`, `StateChanged`, `FlaggedRowPositions`,
      `Disabled`. Every control carries an `aria-label`; removal announces through a live region and
      moves focus per Decision 11.
- [ ] **TASK-018** — Mount `ProductContentCard` in `ProductForm.razor` between the General Info
      panel and `ProductVariantsCard`; wire `DescriptionChanged` and `StateChanged` into `_dirty`
      and the save payload; seed it from `InitialData.Description` and `InitialData.Specifications`;
      flag rows named by `Result.ErrorArgs` after a failed save.
- [ ] **TASK-019** — Add the new UI keys to `Strings.resx` and `Strings.fr.resx`:
      `ProductContent_Heading` ("CONTENT"), `AddProduct_DescriptionCounter`,
      `AddProduct_DescriptionFormattingRemoved`, `ProductSpecifications_Heading` ("Specification"),
      `_AddRow` ("Add Specification"), `_NameLabel`, `_ValueLabel`, `_RemoveRow`,
      `_RowRemovedAnnouncement`, `_EmptyStateTitle`, `_EmptyStateDescription`. No
      `ProductDescription_*` toolbar-label keys — Quill's native `snow` toolbar supplies its own
      (unlocalized) control labels under the Decision 2 waiver. French values for the keys above are
      real translations, not `[TODO]`, because AC-12 is a stated acceptance criterion.
- [ ] **TASK-020** — bUnit component tests for `ProductContentCard` and `ShopRichTextEditor`,
      owned by `shop-test-writer` before Implement completion.

**Completion gate:** matches the Figma nodes above · MudBlazor components only, plus the one
user-approved `ShopRichTextEditor` primitive, whose Quill `snow` toolbar is a further
user-directed waiver of Rules 2, 3, and 19 (Decision 2) · no new NuGet package · no hardcoded
strings or design tokens elsewhere (constitution Rules 11, 13, 15, 16, 19) · `ShopRichTextEditor`
forwards `Class` / `Style` (Rules 23, 24) and holds no `<style>` block or `wwwroot` page stylesheet
(Rule 28) · consumes only frozen contracts · `.sdd/scripts/check-design-rules.ps1` passes on every
changed production file outside the waived toolbar.

### Step 6 — Integration & pipeline

**Depends on:** Steps 4 and 5 complete.

- [ ] **TASK-021** — Cross-layer verification: solution builds, DI resolves, migration `0029`
      applied, and both flows work end to end — create with description plus specifications, and
      edit an existing product whose description predates this feature.
- [ ] **TASK-022** — Test specialists write and run the required unit and component tests before
      Implement completion. **Required boundary case:** because Domain, Application, and
      Infrastructure all execute in the browser under Blazor WebAssembly, no C# check is a trust
      boundary — AC-11 holds only because `products_description_markup_allowed` sits in the
      database. Prove it by calling `save_product` directly with markup outside the Decision 3
      grammar, bypassing `ProductForm` and the validators entirely, and asserting the database
      rejects the write and leaves no row. Also assert one shared grammar fixture set against both
      the C# path and the database, which is what catches the drift accepted in Section 11.
      Record actual counts, member coverage, and remaining AC proof. Follow
      with `/theshop-test product-description`, `/theshop-e2e product-description` (this feature is
      user-facing), and `/theshop-review product-description` when invoked. Document stays a
      separate manual invocation; never schedule it automatically.

### Deviation procedure

- **Accept** a deviation only if it preserves approved behavior and layer boundaries, aligns better
  with existing project conventions, and does not weaken authorization or integrity or expand scope.
- **Reject** it if it changes a requirement, adds business behavior, violates dependency direction,
  silently alters a frozen contract, or smuggles in unrelated refactoring.
- **Contract change:** stop dependent work → record the change here (update the freeze table and
  affected TASK ids) → resume only after the contract is re-frozen.
- **Grammar change:** the Decision 3 grammar appears in three places. Changing it means changing
  `ProductDescription`, both validators, and the `products_description_markup_allowed` `CHECK`
  together, in one deviation record. Never change one.

## 8. Acceptance Criteria → Task Mapping

| AC from spec | Maps to |
|---|---|
| AC-1: create with every FR-1 format plus rows; reopen matches | TASK-001, TASK-002, TASK-004, TASK-007, TASK-012, TASK-013, TASK-016, TASK-017, TASK-018 |
| AC-2: edit description and rows; unrelated details unchanged | TASK-004, TASK-007, TASK-009, TASK-012, TASK-013, TASK-018 |
| AC-3: legacy plain text with angle brackets survives open and save | TASK-001 (`Rehydrate`), TASK-023 (escape migration), TASK-013 |
| AC-4: 2,001 and exactly 20,000 text characters both save | TASK-001, TASK-008, TASK-023 |
| AC-5: 20,001 characters rejected, content retained, product unchanged | TASK-001, TASK-003, TASK-008, TASK-016 |
| AC-6: all description text and all rows removed, empty persists | TASK-001, TASK-004, TASK-008, TASK-012, TASK-017 |
| AC-7: blank or whitespace-only row name or value identified | TASK-002, TASK-003, TASK-008, TASK-017, TASK-018 |
| AC-8: "Material" vs " material " duplicate rejected | TASK-004, TASK-008, TASK-011 (unique index), TASK-014, TASK-018 |
| AC-9: missing or revoked permission refuses create and edit | TASK-011 (RLS policies), TASK-012 (`authorize` guard, unchanged) |
| AC-10: pasted styled text keeps emphasis, drops unsupported styling, notice shown | TASK-016 |
| AC-11: supplied executable content never executes | TASK-001, TASK-008, TASK-014, TASK-015 (`dangerouslyPasteHTML` load path), TASK-016, TASK-023 (`CHECK`) |
| AC-12: English and French interface text | TASK-009, TASK-019 |
| AC-13: keyboard and screen-reader operation, focus after removal | TASK-015, TASK-016, TASK-017 |
| AC-14: unsaved-change warning covers description and rows | TASK-016, TASK-018 |
| AC-15: failed or conflicting save keeps local content, no false success | TASK-007, TASK-013, TASK-014, TASK-018 |

## 9. Validation & Error Handling Strategy

### Validators (Application layer)

- `CreateProductCommandValidator` and `UpdateProductCommandValidator`, identical rules:
  - `Description` matches the Decision 3 grammar (RULE-5, FR-7) → `Product_DescriptionUnsupportedContent`
  - `Description` text length ≤ 20,000 by the Decision 5 rule (RULE-2, FR-3) → `Product_DescriptionTooLong`
  - `Description` `octet_length` ≤ 200,000 (Decision 6) → `Product_DescriptionTooLarge`
  - each `Specifications[i].Name` non-blank after `Trim()` (RULE-3) → `Product_SpecificationNameRequired`
  - each `Specifications[i].Value` non-blank after `Trim()` (RULE-3) → `Product_SpecificationValueRequired`
  - no maximum length applies to a specification `Name` or `Value`. Neither the spec nor an
    existing product rule states one, and the ratified decision is to add none; the columns are
    `TEXT` and the 200,000-byte bound covers `description` only.
  - `Specifications` names distinct after `Trim().ToLowerInvariant()` (RULE-4) → `Product_SpecificationNameDuplicated`
- Empty `Description` and an empty `Specifications` list are valid (FR-3, FR-4, AC-6).

### Domain exceptions

- `ProductDescriptionTooLongException` — thrown when text length exceeds 20,000.
  `MessageKey = "Product_DescriptionTooLong"`. Existing type; only its documentation changes.
- `ProductDescriptionUnsupportedContentException` — thrown when markup falls outside the
  Decision 3 grammar. `MessageKey = "Product_DescriptionUnsupportedContent"`.
- `SpecificationNameRequiredException` — thrown when a retained row's trimmed name is empty.
  `MessageKey = "Product_SpecificationNameRequired"`.
- `SpecificationValueRequiredException` — thrown when a retained row's trimmed value is empty.
  `MessageKey = "Product_SpecificationValueRequired"`.
- `DuplicateSpecificationNameException` — thrown when two normalized names collide.
  `MessageKey = "Product_SpecificationNameDuplicated"`.

### Result.Fail error keys (new entries in `Strings.resx`)

| Key | English text |
|---|---|
| `Product_DescriptionUnsupportedContent` | "The description contains formatting that is not supported. Remove it and try again." |
| `Product_DescriptionTooLarge` | "The description is too large to save. Reduce its formatting and try again." |
| `Product_SpecificationNameRequired` | "Every specification needs a name." |
| `Product_SpecificationValueRequired` | "Every specification needs a value." |
| `Product_SpecificationNameDuplicated` | "Specification names must be unique. Combine the values into one row." |

`Product_DescriptionTooLong` keeps its key and changes its text to "A description cannot exceed
20,000 characters." (French: "La description ne peut pas dépasser 20 000 caractères.")

Every key is mirrored in `Strings.fr.resx` with a real translation, not a `[TODO]` placeholder —
AC-12 makes French an acceptance criterion, not a review-gate cleanup.

## 10. Database Schema & RLS Policies

### Schema

```sql
-- ============================================================================
-- 0029_product_specifications  (TASK-011 — not blocked)
--
-- An ordered name/value child of products, with the same lifecycle as
-- product_option_types. Companion plan: .specs/product-description/plan.md §10
-- ============================================================================

CREATE TABLE product_specifications (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id  UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    name        TEXT NOT NULL CHECK (btrim(name) <> ''),
    value       TEXT NOT NULL CHECK (btrim(value) <> ''),
    position    INTEGER NOT NULL CHECK (position >= 0)
);

CREATE UNIQUE INDEX ux_product_specifications_name
    ON product_specifications(product_id, lower(btrim(name)));
CREATE INDEX idx_product_specifications_product
    ON product_specifications(product_id, position);

-- ============================================================================
-- 0030_product_description_bounds  (TASK-023 — blocked on the TASK-016 capture)
--
-- Bounds products.description to the Decision 3 whitelist grammar. Migrates
-- existing plain-text descriptions to escaped HTML BEFORE adding the CHECKs,
-- so no existing row is rejected. Apply only after Quill's getSemanticHTML()
-- output has been captured and Decision 3's token list frozen against it.
-- Companion plan: .specs/product-description/plan.md §10
-- ============================================================================

-- Step 1 of 2: migrate legacy plain-text descriptions to escaped HTML paragraphs
-- (Decision 7). A row already carrying allowed markup is left alone.
UPDATE products
SET description = '<p>' || replace(
        regexp_replace(
            replace(replace(replace(btrim(description), '&', '&amp;'), '<', '&lt;'), '>', '&gt;'),
            E'\n{2,}', E'\n', 'g'),
        E'\n', '</p><p>') || '</p>'
WHERE btrim(description) <> ''
  AND description !~* '</?(p|br|h[1-6]|strong|em|ol|ul|li|a)\b';

-- Step 2 of 2: bound the column. Every tag token must match the plan's
-- Decision 3 grammar; nothing rewrites content, a bad row is rejected.
ALTER TABLE products
    ADD CONSTRAINT products_description_size
        CHECK (octet_length(description) <= 200000);

ALTER TABLE products
    ADD CONSTRAINT products_description_markup_allowed
        CHECK (NOT EXISTS (
            SELECT 1
            FROM regexp_matches(description, '<[^>]*>', 'g') AS m(tag)
            WHERE m.tag[1] !~* '^</?(p|br|h[1-6]|strong|em|ol|ul|li)\s*/?>$'
              AND m.tag[1] !~* '^</a>$'
              AND m.tag[1] !~* '^<a\s+href="(https?://|mailto:)[^"<>]*"(\s+target="_blank"|\s+rel="noopener noreferrer")*\s*>$'
        ));
```

### RLS policies (the only real security boundary — per `rules/architecture-admin.md`)

```sql
ALTER TABLE product_specifications ENABLE ROW LEVEL SECURITY;

-- Mirrors product_option_types: the customer surface reads the children of
-- PUBLISHED products only; staff with products.view read every row.
CREATE POLICY "product_specifications_public_read" ON product_specifications
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM products p WHERE p.id = product_specifications.product_id AND p.is_published)
        OR (SELECT public.authorize('products.view'))
    );
CREATE POLICY "product_specifications_admin_insert" ON product_specifications
    FOR INSERT WITH CHECK ((SELECT public.authorize('products.create'))
                        OR (SELECT public.authorize('products.edit')));
CREATE POLICY "product_specifications_admin_update" ON product_specifications
    FOR UPDATE USING ((SELECT public.authorize('products.edit')))
    WITH CHECK ((SELECT public.authorize('products.edit')));
CREATE POLICY "product_specifications_admin_delete" ON product_specifications
    FOR DELETE USING ((SELECT public.authorize('products.edit'))
                   OR (SELECT public.authorize('products.create')));
```

`save_product`'s existing `authorize('products.create') OR authorize('products.edit')` guard is
unchanged and continues to cover AC-9 for every path through the RPC.

## 11. Open Questions, Risks & Assumptions

None — all questions resolved. The accepted risks below stay visible; each carries its rationale
and the task that holds its mitigation.

- **⚠️ Risk — ✅ Accepted:** the Decision 3 grammar is stated three times (C# value object, C#
  validators, Postgres `CHECK`), so drift would cause a false rejection or a hole in the boundary.
  Accepted because the copies are small and the token list is fixed: TASK-001 and TASK-023 copy it
  verbatim from Decision 3, and TASK-022 asserts one shared fixture set against both the C# path
  and the database, which is what catches drift.
- **⚠️ Risk — ✅ Accepted:** the project owns the Quill interop lifecycle rather than a package, so
  a leaked `DotNetObjectReference` or `IJSObjectReference` on repeated Add Product / Edit Product
  navigation would accumulate detached editors. Accepted because the failure mode is a memory leak
  rather than data loss, and the treatment is standard: TASK-015 implements `IAsyncDisposable` and
  TASK-020 covers mount and unmount in bUnit.
- **⚠️ Risk — ✅ Accepted:** vendored Quill 2.0.3 receives no automatic updates, so a future Quill
  security fix is a manual bump. Accepted because the version is pinned in one place
  (`wwwroot/lib/quill/`) and named in this plan, and is reviewed like any other pinned dependency.
- **⚠️ Risk — ✅ Accepted:** TASK-023's escaping `UPDATE` rewrites every existing
  `products.description` and has no down migration, so a wrong regex would corrupt live product
  copy. Accepted on the condition that it is rehearsed: run it against a Supabase branch first,
  compare row counts and a sample diff, and take a `products` backup before applying to the primary
  project.
- **⚠️ Risk — ✅ Accepted:** rendering the stored HTML on the storefront is out of this feature's
  scope (spec Section 1), but a stored-XSS defect would surface there. Accepted as a scope handoff:
  this plan bounds what can be stored, and the product detail page scope owns rendering it through
  the same whitelist.
---
**Status:** Resolved · **Spec:** `.specs/product-description/spec.md` · **Created:** 2026-09-10 · **Resolved:** 2026-09-10
