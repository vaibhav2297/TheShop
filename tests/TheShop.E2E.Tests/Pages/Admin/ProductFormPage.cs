using Microsoft.Playwright;
using TheShop.Web.Resources;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>
/// Page object for the shared add/edit product form (<c>ProductForm.razor</c>), reached from
/// either <c>/admin/products/new</c> or <c>/admin/products/{id}/edit</c> — both render the
/// identical component, so this object is not route-tied (the same pattern as
/// <c>EditCategoryPage</c>). Covers the General Info fields, the image gallery, and the inline
/// Variants card (.specs/create-product/spec.md).
/// </summary>
public sealed class ProductFormPage(IPage page)
{
    private IPage Page { get; } = page;

    // ---- General Info -----------------------------------------------------------------------

    public ILocator NameField => Page.GetByTestId("product-name");
    public ILocator SkuField => Page.GetByTestId("product-sku");
    public ILocator DescriptionField => Page.GetByLabel(Strings.AddProduct_DescriptionLabel, new() { Exact = true });

    // Exact match is required here: "Price" is a substring of "Sale Price", so a non-exact
    // GetByLabel("Price") resolves to both fields and Playwright refuses the ambiguity.
    public ILocator PriceField => Page.GetByLabel(Strings.AddProduct_PriceLabel, new() { Exact = true });
    public ILocator SalePriceField => Page.GetByLabel(Strings.AddProduct_SalePriceLabel, new() { Exact = true });
    public ILocator SaveButton => Page.GetByTestId("product-save");
    public ILocator CancelButton => Page.GetByRole(AriaRole.Link, new() { Name = Strings.AddProduct_CancelButton });

    public Task FillNameAsync(string name) => NameField.FillAsync(name);
    public Task FillDescriptionAsync(string description) => DescriptionField.FillAsync(description);
    public Task SetPriceAsync(decimal amount) => FillAndBlurAsync(PriceField, amount.ToString("0.00"));
    public Task SetSalePriceAsync(decimal amount) => FillAndBlurAsync(SalePriceField, amount.ToString("0.00"));
    public Task SaveAsync() => SaveButton.ClickAsync();

    /// <summary>
    /// <c>ShopMoneyField</c> (and the option-type name/value fields below) bind Value/ValueChanged
    /// without <c>Immediate="true"</c>, so MudBlazor only raises <c>ValueChanged</c> on blur.
    /// <c>FillAsync</c> alone sets the DOM input's value and dispatches an input event, which is
    /// enough to make the raw element read back the typed text but not enough to reach the
    /// component's C# state — an assertion against the input's own value would pass while the
    /// bound field, and everything downstream of it (form validity, computed totals), stays
    /// unchanged. An explicit blur after every fill is what commits the value for real.
    /// </summary>
    private static async Task FillAndBlurAsync(ILocator field, string value)
    {
        await field.FillAsync(value);
        await field.EvaluateAsync("el => el.blur()");
    }

    public async Task SelectCategoryAsync(string categoryName)
    {
        await Page.GetByLabel(Strings.AddProduct_CategoryLabel).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = categoryName }).ClickAsync();
    }

    public async Task SelectBrandAsync(string brandName)
    {
        await Page.GetByLabel(Strings.AddProduct_BrandLabel).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = brandName }).ClickAsync();
    }

    /// <summary>
    /// The product-level publish control, scoped to the chip pair immediately following the
    /// "Status" label — anchoring here (rather than a bare text match) is what keeps this from
    /// colliding with a variant row's own Active/Inactive availability chip, which reuses the
    /// identical two strings (spec RULE-16 vs. FR-9 are unrelated concepts sharing UI copy).
    /// </summary>
    private ILocator StatusControl =>
        Page.GetByText(Strings.AddProduct_StatusLabel, new() { Exact = true })
            .Locator("xpath=following-sibling::*[1]");

    public ILocator PublishedChip => StatusControl.GetByText(Strings.AddProduct_StatusActive, new() { Exact = true });
    public ILocator UnpublishedChip => StatusControl.GetByText(Strings.AddProduct_StatusInactive, new() { Exact = true });
    public Task SetPublishedAsync() => PublishedChip.ClickAsync();
    public Task SetUnpublishedAsync() => UnpublishedChip.ClickAsync();

    public ILocator PriceRangeNote => Page.GetByText(Strings.AddProduct_PriceRangeNote, new() { Exact = true });
    public ILocator PriceRangeNoteWithValue(string formattedPrice) =>
        Page.GetByText(string.Format(Strings.AddProduct_PriceRangeNoteWithValue, formattedPrice), new() { Exact = true });

    // ---- Gallery -----------------------------------------------------------------------------

    /// <summary>
    /// Selects files through the underlying (hidden) file input. Scoped to the gallery's own
    /// project class rather than a bare element type, since <c>SetInputFilesAsync</c> does not
    /// require visibility but a page could in principle host more than one file input.
    /// </summary>
    public Task UploadImagesAsync(params FilePayload[] files) =>
        Page.Locator(".shop-image-upload input[type=file]").SetInputFilesAsync(files);

    /// <summary>An uploaded image's preview row, located by its filename caption.</summary>
    public ILocator ImageRow(string fileName) =>
        Page.Locator(".shop-image-upload .preview").Filter(new() { Has = Page.GetByText(fileName, new() { Exact = true }) });

    public ILocator ImageRows => Page.Locator(".shop-image-upload .preview");

    public ILocator PrimaryBadge(string fileName) =>
        ImageRow(fileName).GetByText(Strings.AddProduct_ImagePrimary, new() { Exact = true });

    public Task RemoveImageAsync(string fileName) =>
        ImageRow(fileName).GetByRole(AriaRole.Button, new() { Name = Strings.AddProduct_ImageRemove }).ClickAsync();

    public ILocator ImageError(string fileName) => ImageRow(fileName).GetByText(new System.Text.RegularExpressions.Regex(".+"));

    // ---- Variants card -------------------------------------------------------------------------

    public ILocator AddOptionTypeButton => Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_AddVariant });
    public Task AddOptionTypeAsync() => AddOptionTypeButton.ClickAsync();

    /// <summary>The Nth option type's name field, by creation order (0-based).</summary>
    public ILocator OptionTypeNameField(int index) => Page.GetByPlaceholder(Strings.ProductVariants_OptionNameLabel).Nth(index);

    public Task FillOptionTypeNameAsync(int index, string name) => FillAndBlurAsync(OptionTypeNameField(index), name);

    /// <summary>The Nth option type's own "Add Value" button (0-based, one per option type).</summary>
    public ILocator AddOptionValueButton(int optionTypeIndex) =>
        Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_AddValue }).Nth(optionTypeIndex);

    public Task AddOptionValueAsync(int optionTypeIndex) => AddOptionValueButton(optionTypeIndex).ClickAsync();

    /// <summary>
    /// An option value field, addressed by its position across every option type's values in
    /// creation order (0-based) — the placeholder text is shared by every value field on the
    /// page, so tests must fill values in the same order they were added.
    /// </summary>
    public ILocator OptionValueField(int globalValueIndex) =>
        Page.GetByPlaceholder(Strings.ProductVariants_OptionValuesLabel).Nth(globalValueIndex);

    public Task FillOptionValueAsync(int globalValueIndex, string value) =>
        FillAndBlurAsync(OptionValueField(globalValueIndex), value);

    public ILocator RemoveOptionTypeButton(int optionTypeIndex) =>
        Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_RemoveOptionType }).Nth(optionTypeIndex);

    public Task RemoveOptionTypeAsync(int optionTypeIndex) => RemoveOptionTypeButton(optionTypeIndex).ClickAsync();

    public ILocator RemoveOptionValueButton(int globalValueIndex) =>
        Page.GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_RemoveOptionValue }).Nth(globalValueIndex);

    public Task RemoveOptionValueAsync(int globalValueIndex) => RemoveOptionValueButton(globalValueIndex).ClickAsync();

    public ILocator VariantCount(int count) =>
        Page.GetByText(string.Format(Strings.ProductVariants_With_Args, count), new() { Exact = true });

    public ILocator MissingPriceAlert(int missing, int total) =>
        Page.GetByText(string.Format(Strings.ProductVariants_MissingPrice, missing, total), new() { Exact = true });

    /// <summary>A generated variant's row, located by its label (e.g. "Mango / 20mg").</summary>
    public ILocator VariantRow(string label) =>
        Page.Locator("tr").Filter(new() { Has = Page.GetByText(label, new() { Exact = true }) });

    public ILocator VariantSku(string label) => VariantRow(label).GetByRole(AriaRole.Cell).Nth(1);

    /// <summary>The variant row's price input — the row's first of its (at most) two numeric inputs.</summary>
    public ILocator VariantPriceInput(string label) => VariantRow(label).Locator("input").Nth(0);

    /// <summary>The variant row's sale-price input — the row's second numeric input.</summary>
    public ILocator VariantSalePriceInput(string label) => VariantRow(label).Locator("input").Nth(1);

    public Task SetVariantPriceAsync(string label, decimal amount) =>
        FillAndBlurAsync(VariantPriceInput(label), amount.ToString("0.00"));
    public Task SetVariantSalePriceAsync(string label, decimal amount) =>
        FillAndBlurAsync(VariantSalePriceInput(label), amount.ToString("0.00"));

    public ILocator VariantAvailabilityChip(string label, bool available) =>
        VariantRow(label).GetByText(
            available ? Strings.AddProduct_StatusActive : Strings.AddProduct_StatusInactive,
            new() { Exact = true });

    public Task ToggleVariantAvailabilityAsync(string label, bool currentlyAvailable) =>
        VariantAvailabilityChip(label, currentlyAvailable).ClickAsync();

    public ILocator VariantPinButton(string label) =>
        VariantRow(label).GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_PinImage });

    public ILocator VariantChangeImageButton(string label) =>
        VariantRow(label).GetByRole(AriaRole.Button, new() { Name = Strings.ProductVariants_ChangeImage });

    public Task OpenPinDialogAsync(string label) =>
        (VariantRow(label).GetByRole(AriaRole.Button, new()
        {
            NameRegex = new System.Text.RegularExpressions.Regex(
                $"^({System.Text.RegularExpressions.Regex.Escape(Strings.ProductVariants_PinImage)}|{System.Text.RegularExpressions.Regex.Escape(Strings.ProductVariants_ChangeImage)})$"),
        })).ClickAsync();

    // ---- Variant image pin dialog (VariantImageDialog) ----------------------------------------

    private ILocator DialogSurface => Page.Locator(".mud-dialog");

    /// <summary>A selectable gallery tile inside the pin dialog, located by its alt text (the variant label).</summary>
    public ILocator PinDialogImages => DialogSurface.Locator("img");

    public Task SelectPinDialogImageAsync(int index) => PinDialogImages.Nth(index).ClickAsync();

    public ILocator ApplyToAllSharingCheckbox(int sharedCount, string sharedScopeLabel) =>
        DialogSurface.GetByLabel(string.Format(Strings.VariantImage_ApplyToAllSharing, sharedCount, sharedScopeLabel));

    public ILocator ThisVariantOnlyCheckbox => DialogSurface.GetByLabel(Strings.VariantImage_ThisVariantOnly);

    public ILocator PinDialogSaveButton => DialogSurface.GetByRole(AriaRole.Button, new() { Name = Strings.VariantImage_Save });

    public Task ConfirmPinDialogAsync() => PinDialogSaveButton.ClickAsync();

    // ---- Remove-option confirmation (ShopConfirmDialog) -----------------------------------------

    public ConfirmDialogPage RemoveOptionConfirm => new(Page);
}
