using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Domain.ValueObjects;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The product form's inline Variants card (Figma node <c>2687:14723</c>/<c>2687:15451</c>):
/// the option-type editor (add/rename/remove option types and values, with a RULE-11
/// confirmation before discarding configured variants) and the generated variant table,
/// including the image pin (FR-16, AC-10a). The client recomputes the cartesian product of
/// option values purely for immediate feedback — <c>Product.ApplyVariantConfiguration</c> is
/// the real truth server-side (Flow 4 step 4).
/// </summary>
public partial class ProductVariantsCard : MudComponentBase
{
    [Inject] private IDialogService DialogService { get; set; } = default!;

    /// <summary>
    /// The product's own SKU, from which every variant SKU is derived (FR-15). Read live on every
    /// parameter set so a variant's SKU stays in step with the product's as its name changes.
    /// </summary>
    [Parameter, EditorRequired]
    public string ProductSku { get; set; } = string.Empty;

    /// <summary>The product's current gallery, offered by the pin dialog.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ProductImageDto> GalleryImages { get; set; } = [];

    /// <summary>The saved option types, loaded once (Edit mode); empty for a new product.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ProductOptionTypeDto> InitialOptionTypes { get; set; } = [];

    /// <summary>The saved variants, loaded once (Edit mode); empty for a new product.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ProductVariantDto> InitialVariants { get; set; } = [];

    /// <summary>Fires with the card's current option types and variants whenever either changes.</summary>
    [Parameter, EditorRequired]
    public EventCallback<ProductVariantsState> StateChanged { get; set; }

    /// <summary>
    /// Whether every variant needs a price, which is true exactly while the product is set to
    /// publish (RULE-14). A draft may be saved with variants half-configured (RULE-15).
    /// </summary>
    [Parameter] public bool RequirePrices { get; set; }

    /// <summary>
    /// Variants the form has flagged as missing a price, so their rows show the problem where the
    /// staff member has to fix it rather than only in a message above the table (AC-20). The table
    /// is virtualized, so this — not the enclosing form's own validation, which never sees an
    /// unrendered row — is what makes an off-screen omission visible once scrolled to.
    /// </summary>
    [Parameter] public IReadOnlyList<Guid> FlaggedVariantIds { get; set; } = [];

    [Parameter] public bool Disabled { get; set; }

    private sealed class OptionValueRow
    {
        public required Guid Id { get; init; }
        public string Value { get; set; } = string.Empty;
    }

    private sealed class OptionTypeRow
    {
        public required Guid Id { get; init; }
        public string Name { get; set; } = string.Empty;
        public List<OptionValueRow> Values { get; set; } = [];
    }

    private sealed class VariantRow
    {
        public required Guid Id { get; init; }
        public required IReadOnlySet<Guid> OptionValueIds { get; init; }
        public string Sku { get; set; } = string.Empty;
        public decimal? OriginalPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public bool IsAvailable { get; set; } = true;
        public Guid? PinnedImageId { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private readonly List<OptionTypeRow> _optionTypes = [];
    private List<VariantRow> _variants = [];
    private bool _initialized;
    private string? _lastProductSku;

    private bool IsFlagged(VariantRow variant) => FlaggedVariantIds.Contains(variant.Id);

    // The copy-to-all header buttons (RULE-14) act on the first row, so they only make sense once
    // there is a second row to receive it, and once the first row actually has something to give.
    private bool CanCopyPriceToAll => _variants.Count > 1 && _variants[0].OriginalPrice is not null;
    private bool CanCopySalePriceToAll => _variants.Count > 1 && _variants[0].SalePrice is not null;

    protected string Classname => new CssBuilder("product-variants-card").AddClass(Class).Build();
    protected string Stylename => new StyleBuilder().AddStyle(Style).Build();

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        if (!_initialized)
            LoadInitialState();

        // The product's own SKU is itself auto-generated from its name and can change on every
        // keystroke (Decision 7), so every variant SKU — built from that SKU plus the variant's own
        // option values (FR-15) — is kept live rather than fixed at the moment the row was created.
        if (_lastProductSku != ProductSku)
        {
            _lastProductSku = ProductSku;
            RefreshVariantSkus();
        }

        await PruneRemovedPinsAsync();
    }

    private void RefreshVariantSkus()
    {
        foreach (var variant in _variants)
            variant.Sku = SuggestVariantSku(variant.OptionValueIds);
    }

    private void LoadInitialState()
    {
        _initialized = true;

        foreach (var type in InitialOptionTypes.OrderBy(t => t.Position))
        {
            _optionTypes.Add(new OptionTypeRow
            {
                Id = type.Id,
                Name = type.Name,
                Values = [.. type.Values.OrderBy(v => v.Position).Select(v => new OptionValueRow { Id = v.Id, Value = v.Value })],
            });
        }

        _variants = [.. InitialVariants.Select(v => new VariantRow
        {
            Id = v.Id,
            OptionValueIds = new HashSet<Guid>(v.OptionValueIds),
            Sku = v.Sku,
            OriginalPrice = v.OriginalPrice,
            SalePrice = v.SalePrice,
            IsAvailable = v.IsAvailable,
            PinnedImageId = v.PinnedImageId,
            Label = v.Label,
        })];
    }

    /// <summary>
    /// Drops any pin whose image has left the gallery, the client-side half of RULE-13 — the staff
    /// member can remove an image they pinned moments earlier, before either has been saved. The
    /// aggregate enforces the same rule server-side for images removed in an earlier session.
    /// </summary>
    private Task PruneRemovedPinsAsync()
    {
        var available = GalleryImages.Select(image => image.Id).ToHashSet();
        var stale = _variants
            .Where(v => v.PinnedImageId is Guid pinned && !available.Contains(pinned))
            .ToList();

        if (stale.Count == 0)
            return Task.CompletedTask;

        foreach (var variant in stale)
            variant.PinnedImageId = null;

        return NotifyAsync();
    }

    private Task AddOptionTypeAsync()
    {
        var row = new OptionTypeRow
        {
            Id = Guid.NewGuid(),
            Values = [new OptionValueRow { Id = Guid.NewGuid() }],
        };
        _optionTypes.Add(row);
        return NotifyAsync();
    }

    private Task OnOptionTypeNameChangedAsync(OptionTypeRow type, string value)
    {
        type.Name = value;
        return NotifyAsync();
    }

    private async Task RemoveOptionTypeAsync(OptionTypeRow type)
    {
        var remaining = _optionTypes.Where(t => t.Id != type.Id).ToList();
        var affected = CountConfiguredVariantsLostBy(remaining);

        if (affected > 0 && !await ConfirmRemovalAsync(affected))
            return;

        _optionTypes.Remove(type);
        RecomputeVariants();
        await NotifyAsync();
    }

    private Task AddOptionValueAsync(OptionTypeRow type)
    {
        type.Values.Add(new OptionValueRow { Id = Guid.NewGuid() });
        return NotifyAsync();
    }

    private Task OnOptionValueChangedAsync(OptionValueRow value, string newValue)
    {
        value.Value = newValue;
        RecomputeVariants();
        return NotifyAsync();
    }

    private async Task RemoveOptionValueAsync(OptionTypeRow type, OptionValueRow value)
    {
        if (type.Values.Count == 1 && type.Values[0].Id == value.Id)
        {
            await RemoveOptionTypeAsync(type);
            return;
        }

        var candidate = _optionTypes.Select(t => t.Id == type.Id
                ? new OptionTypeRow { Id = t.Id, Name = t.Name, Values = [.. t.Values.Where(v => v.Id != value.Id)] }
                : t)
            .ToList();
        var affected = CountConfiguredVariantsLostBy(candidate);

        if (affected > 0 && !await ConfirmRemovalAsync(affected))
            return;

        type.Values.Remove(value);
        RecomputeVariants();
        await NotifyAsync();
    }

    private Task OnVariantOriginalPriceChangedAsync(VariantRow variant, decimal? value)
    {
        variant.OriginalPrice = value;
        return NotifyAsync();
    }

    private Task OnVariantSalePriceChangedAsync(VariantRow variant, decimal? value)
    {
        variant.SalePrice = value;
        return NotifyAsync();
    }

    /// <summary>
    /// Copies the first variant row's price onto every other row. The product itself deliberately
    /// has no price that variants fall back to — a variant left unpriced has to be corrected, not
    /// quietly filled in — so this is the explicit way to price a whole set at once, and what it
    /// writes is visible in every row afterwards.
    /// </summary>
    private Task CopyPriceToAllVariantsAsync()
    {
        if (_variants.Count == 0 || _variants[0].OriginalPrice is not decimal price)
            return Task.CompletedTask;

        foreach (var variant in _variants.Skip(1))
            variant.OriginalPrice = price;

        return NotifyAsync();
    }

    /// <summary>
    /// Copies the first variant row's sale price onto every other row. Unlike the price column, a
    /// sale price left blank is a valid "not on sale" state — copying it down is still done row-by-
    /// row so it never overwrites the first row itself.
    /// </summary>
    private Task CopySalePriceToAllVariantsAsync()
    {
        if (_variants.Count == 0 || _variants[0].SalePrice is not decimal price)
            return Task.CompletedTask;

        foreach (var variant in _variants.Skip(1))
            variant.SalePrice = price;

        return NotifyAsync();
    }

    private Task OnVariantAvailableChangedAsync(VariantRow variant, bool value)
    {
        variant.IsAvailable = value;
        return NotifyAsync();
    }

    private async Task OpenPinDialogAsync(VariantRow variant)
    {
        var (scopeLabel, scopeCount) = FindSharedScope(variant);

        var parameters = new DialogParameters<VariantImageDialog>
        {
            { x => x.VariantLabel, variant.Label },
            { x => x.GalleryImages, GalleryImages },
            { x => x.CurrentImageId, variant.PinnedImageId },
            { x => x.SharedScopeLabel, scopeLabel },
            { x => x.SharedScopeCount, scopeCount },
        };

        // The gallery grid needs more room than the default dialog width (Small) affords.
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };

        var dialog = await DialogService.ShowAsync<VariantImageDialog>(variant.Label, parameters, options);
        var result = await dialog.Result;
        if (result is not { Canceled: false, Data: VariantImagePinResult pin })
            return;

        if (pin.ApplyToAllSharing)
        {
            var sharedValueId = FindSharedOptionValueId(variant);
            if (sharedValueId is Guid valueId)
            {
                foreach (var affected in _variants.Where(v => v.OptionValueIds.Contains(valueId)))
                    affected.PinnedImageId = pin.ImageId;
            }
        }
        else
        {
            variant.PinnedImageId = pin.ImageId;
        }

        await NotifyAsync();
    }

    /// <summary>
    /// Finds this variant's first option value shared by at least one sibling, for the pin
    /// dialog's "Apply To" scope (a defensible simplification for products with more than one
    /// option type — the fan-out targets one option value at a time).
    /// </summary>
    private (string? Label, int Count) FindSharedScope(VariantRow variant)
    {
        foreach (var type in _optionTypes)
        {
            var value = type.Values.FirstOrDefault(v => variant.OptionValueIds.Contains(v.Id));
            if (value is null)
                continue;

            var count = _variants.Count(v => v.OptionValueIds.Contains(value.Id));
            if (count > 1)
                return ($"{type.Name} = {value.Value}", count);
        }

        return (null, 0);
    }

    private Guid? FindSharedOptionValueId(VariantRow variant)
    {
        foreach (var type in _optionTypes)
        {
            var value = type.Values.FirstOrDefault(v => variant.OptionValueIds.Contains(v.Id));
            if (value is null)
                continue;

            if (_variants.Count(v => v.OptionValueIds.Contains(value.Id)) > 1)
                return value.Id;
        }

        return null;
    }

    private async Task<bool> ConfirmRemovalAsync(int affectedCount)
    {
        var parameters = new DialogParameters<ShopConfirmDialog>
        {
            { x => x.TitleText, Strings.ProductVariants_RemoveOptionConfirmTitle },
            { x => x.BodyText, string.Format(Strings.ProductVariants_RemoveOptionConfirmBody, affectedCount) },
            { x => x.ConfirmLabel, Strings.ProductVariants_RemoveOptionConfirm },
            { x => x.ConfirmColor, Color.Error },
        };

        var dialog = await DialogService.ShowAsync<ShopConfirmDialog>(Strings.ProductVariants_RemoveOptionConfirmTitle, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    private void RecomputeVariants()
    {
        if (_optionTypes.Count == 0)
        {
            _variants = [];
            return;
        }

        var combinations = CartesianProductFor(_optionTypes);
        var existingByKey = _variants.ToDictionary(v => Key(v.OptionValueIds));
        var newVariants = new List<VariantRow>();

        foreach (var combo in combinations)
        {
            var key = Key(combo);
            if (existingByKey.TryGetValue(key, out var existing))
            {
                existing.Label = BuildLabel(combo);
                existing.Sku = SuggestVariantSku(combo);
                newVariants.Add(existing);
            }
            else
            {
                newVariants.Add(new VariantRow
                {
                    Id = Guid.NewGuid(),
                    OptionValueIds = combo,
                    Sku = SuggestVariantSku(combo),
                    IsAvailable = true,
                    Label = BuildLabel(combo),
                });
            }
        }

        _variants = newVariants;
    }

    private int CountConfiguredVariantsLostBy(List<OptionTypeRow> candidateTypes)
    {
        if (candidateTypes.Count == 0)
            return _variants.Count(IsConfigured);

        var newCombos = CartesianProductFor(candidateTypes).Select(Key).ToHashSet();
        return _variants.Count(v => IsConfigured(v) && !newCombos.Contains(Key(v.OptionValueIds)));
    }

    private static bool IsConfigured(VariantRow v) => v.OriginalPrice.HasValue;

    private static List<HashSet<Guid>> CartesianProductFor(List<OptionTypeRow> types)
    {
        IEnumerable<HashSet<Guid>> combinations = [[]];
        foreach (var type in types)
        {
            combinations = combinations.SelectMany(existing =>
                type.Values.Where(v => !string.IsNullOrWhiteSpace(v.Value)).Select(value =>
                {
                    var next = new HashSet<Guid>(existing) { value.Id };
                    return next;
                }));
        }

        return [.. combinations];
    }

    private string BuildLabel(IReadOnlySet<Guid> combo)
    {
        var labels = new List<string>();
        foreach (var type in _optionTypes)
        {
            var value = type.Values.FirstOrDefault(v => combo.Contains(v.Id));
            if (value is not null)
                labels.Add(value.Value);
        }

        return string.Join(" / ", labels);
    }

    private string SuggestVariantSku(IReadOnlySet<Guid> combo)
    {
        var labels = new List<string>();
        foreach (var type in _optionTypes)
        {
            var value = type.Values.FirstOrDefault(v => combo.Contains(v.Id));
            if (value is not null)
                labels.Add(value.Value);
        }

        if (string.IsNullOrWhiteSpace(ProductSku))
            return string.Empty;

        return Sku.Suggest(Sku.Create(ProductSku), labels).Value;
    }

    private static string Key(IReadOnlySet<Guid> ids) => string.Join(",", ids.OrderBy(id => id));

    private Task NotifyAsync()
    {
        var optionTypeInputs = _optionTypes
            .Select(t => new OptionTypeInput(t.Id, t.Name, [.. t.Values.Select(v => new OptionValueInput(v.Id, v.Value))]))
            .ToList();

        var variantInputs = _variants
            .Select(v => new VariantInput(
                v.Id, [.. v.OptionValueIds], v.Sku, v.OriginalPrice, v.SalePrice, v.IsAvailable, v.PinnedImageId))
            .ToList();

        var labels = _variants.ToDictionary(v => v.Id, v => v.Label);

        return StateChanged.InvokeAsync(new ProductVariantsState(optionTypeInputs, variantInputs, labels));
    }
}
