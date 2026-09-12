using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// A catalogue product. Owns its gallery (<see cref="Images"/>), option types
/// (<see cref="OptionTypes"/>), specification rows (<see cref="Specifications"/>), and
/// generated variants (<see cref="Variants"/>) as a single aggregate; every cross-child
/// invariant — one primary image, variants as the cartesian product of option values, a pin
/// resolving only to this product's own gallery, publish completeness — is enforced here
/// rather than by a handler.
/// </summary>
public sealed class Product
{
    private const int MaxNameLength = 150;

    private readonly List<ProductImage> _images = [];
    private readonly List<ProductOptionType> _optionTypes = [];
    private readonly List<ProductSpecification> _specifications = [];
    private readonly List<ProductVariant> _variants = [];

    // A read-optimized rehydration (the customer catalogue / admin list) carries the DB's
    // trigger-maintained min_variant_price / has_sellable_variant columns directly instead of
    // embedding every variant row, which would be too expensive for a paged list when a single
    // product can carry an uncapped variant count (AC-15). These back the computed properties
    // below only when _variants is empty; once variants are loaded (admin edit, or an
    // in-memory aggregate right after Create/ApplyVariantConfiguration) the children are truth.
    private readonly bool _hasVariantsFlag;
    private readonly Money? _minVariantPriceStored;
    private readonly bool _hasSellableVariantStored;

    public Guid Id { get; }
    public string Name { get; private set; }
    public ProductDescription Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public Sku Sku { get; private set; }
    public ProductPricing? Pricing { get; private set; }
    public bool IsPublished { get; private set; }
    public Category Category { get; private set; }
    public Brand Brand { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();
    public IReadOnlyList<ProductOptionType> OptionTypes => _optionTypes.AsReadOnly();
    public IReadOnlyList<ProductSpecification> Specifications => _specifications.AsReadOnly();
    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    /// <summary>
    /// <c>true</c> once at least one option type has produced variants (AC-9).
    /// </summary>
    public bool HasVariants => _variants.Count > 0 || _hasVariantsFlag;

    /// <summary>
    /// <c>true</c> when the product currently has a sale price. <c>false</c> for a draft with no
    /// pricing yet (RULE-15).
    /// </summary>
    public bool IsDiscounted => Pricing?.IsDiscounted ?? false;

    /// <summary>
    /// The price to charge and display prominently: the sale price when discounted, otherwise
    /// the original price. <c>null</c> for a draft with no pricing yet (RULE-15).
    /// </summary>
    public Money? EffectivePrice => Pricing?.Effective;

    /// <summary>
    /// <c>true</c> when the product has something to sell: <see cref="HasSellableVariant"/> when
    /// it has variants, or always <c>true</c> otherwise — stock is not tracked (RULE-16 revision).
    /// </summary>
    public bool IsInStock => !HasVariants || HasSellableVariant;

    /// <summary>
    /// The lowest effective price across the product's variants, or <c>null</c> when it has no
    /// variants or none are priced yet (AC-17).
    /// </summary>
    public Money? MinVariantPrice =>
        _variants.Count > 0
            ? _variants
                .Select(v => v.Pricing?.Effective)
                .Where(price => price is not null)
                .Select(price => price!)
                .OrderBy(price => price.Amount)
                .FirstOrDefault()
            : (_hasVariantsFlag ? _minVariantPriceStored : null);

    /// <summary>
    /// <c>true</c> when the product can currently be sold: an available variant when it has
    /// variants, or always <c>true</c> otherwise — stock is not tracked (RULE-16 revision, AC-29).
    /// </summary>
    public bool HasSellableVariant =>
        _variants.Count > 0
            ? _variants.Any(v => v.IsAvailable)
            : !_hasVariantsFlag || _hasSellableVariantStored;

    private Product(
        Guid id,
        string name,
        ProductDescription description,
        string? imageUrl,
        Sku sku,
        ProductPricing? pricing,
        bool isPublished,
        Category category,
        Brand brand,
        DateTimeOffset createdAt,
        bool hasVariantsFlag,
        Money? minVariantPriceStored,
        bool hasSellableVariantStored)
    {
        Id = id;
        Name = name;
        Description = description;
        ImageUrl = imageUrl;
        Sku = sku;
        Pricing = pricing;
        IsPublished = isPublished;
        Category = category;
        Brand = brand;
        CreatedAt = createdAt;
        _hasVariantsFlag = hasVariantsFlag;
        _minVariantPriceStored = minVariantPriceStored;
        _hasSellableVariantStored = hasSellableVariantStored;
    }

    /// <summary>
    /// Creates a new <see cref="Product"/> with an empty gallery, no option types, no
    /// specifications, and no variants. Gallery, option types, specifications, and variants are
    /// attached afterward via <see cref="SetGallery"/>, <see cref="SetOptionTypes"/>,
    /// <see cref="SetSpecifications"/>, and <see cref="ApplyVariantConfiguration"/>.
    /// </summary>
    /// <exception cref="ProductNameRequiredException">The trimmed name is empty.</exception>
    /// <exception cref="ProductNameTooLongException">The trimmed name exceeds 150 characters.</exception>
    public static Product Create(
        string name,
        ProductDescription description,
        string? imageUrl,
        Sku sku,
        ProductPricing? pricing,
        bool isPublished,
        Category category,
        Brand brand,
        DateTimeOffset? createdAt = null)
    {
        var trimmedName = ValidateName(name);

        return new Product(
            Guid.NewGuid(), trimmedName, description, imageUrl, sku, pricing,
            isPublished, category, brand, createdAt ?? DateTimeOffset.UtcNow,
            hasVariantsFlag: false, minVariantPriceStored: null, hasSellableVariantStored: false);
    }

    /// <summary>
    /// Reconstructs a <see cref="Product"/> from persisted data without re-running creation
    /// invariants, including its gallery, option types, specifications, and variants. When the
    /// caller does not embed <paramref name="variants"/> (a read-optimized list projection),
    /// pass <paramref name="hasVariants"/> / <paramref name="minVariantPrice"/> /
    /// <paramref name="hasSellableVariant"/> from the DB's precomputed read-model columns instead.
    /// </summary>
    public static Product Rehydrate(
        Guid id,
        string name,
        ProductDescription description,
        string? imageUrl,
        Sku sku,
        ProductPricing? pricing,
        bool isPublished,
        Category category,
        Brand brand,
        DateTimeOffset createdAt,
        IReadOnlyList<ProductImage>? images = null,
        IReadOnlyList<ProductOptionType>? optionTypes = null,
        IReadOnlyList<ProductSpecification>? specifications = null,
        IReadOnlyList<ProductVariant>? variants = null,
        bool hasVariants = false,
        Money? minVariantPrice = null,
        bool hasSellableVariant = false)
    {
        var product = new Product(
            id, name, description, imageUrl, sku, pricing, isPublished, category, brand, createdAt,
            hasVariantsFlag: hasVariants, minVariantPriceStored: minVariantPrice, hasSellableVariantStored: hasSellableVariant);

        if (images is not null)
            product._images.AddRange(images);
        if (optionTypes is not null)
            product._optionTypes.AddRange(optionTypes);
        if (specifications is not null)
            product._specifications.AddRange(specifications);
        if (variants is not null)
            product._variants.AddRange(variants);

        return product;
    }

    /// <summary>
    /// Updates the product's name, description, category, and brand, re-validating the name
    /// invariant <see cref="Create"/> enforces. <paramref name="description"/> arrives already
    /// validated by <see cref="ProductDescription.Create"/>.
    /// </summary>
    /// <exception cref="ProductNameRequiredException">The trimmed name is empty.</exception>
    /// <exception cref="ProductNameTooLongException">The trimmed name exceeds 150 characters.</exception>
    public void UpdateDetails(string name, ProductDescription description, Category category, Brand brand)
    {
        Name = ValidateName(name);
        Description = description;
        Category = category;
        Brand = brand;
    }

    /// <summary>
    /// Assigns the product's own SKU.
    /// </summary>
    public void SetSku(Sku sku) => Sku = sku;

    /// <summary>
    /// Sets the product's own pricing. <c>null</c> leaves it a draft (RULE-15).
    /// </summary>
    public void SetPricing(ProductPricing? pricing) => Pricing = pricing;

    /// <summary>
    /// Sets the Published/Unpublished status directly (AC-30). Publishing itself is still
    /// gated by <see cref="EnsurePublishable"/>, called separately by the handler.
    /// </summary>
    public void SetPublished(bool isPublished) => IsPublished = isPublished;

    /// <summary>
    /// Replaces the product's gallery wholesale from the staff member's current gallery state —
    /// existing images kept by id, new uploads with <c>Id: null</c>. Re-asserts RULE-7's
    /// "exactly one primary whenever any image exists", defaulting to the first entry if the
    /// caller supplied none or more than one.
    /// </summary>
    public void SetGallery(IReadOnlyList<GalleryImageInput> images)
    {
        _images.Clear();
        var position = 0;
        foreach (var input in images)
        {
            var image = input.Id is Guid id
                ? ProductImage.Rehydrate(id, input.ObjectKey, position, input.IsPrimary)
                : ProductImage.Create(input.ObjectKey, position, input.IsPrimary);
            _images.Add(image);
            position++;
        }

        EnsureExactlyOnePrimary();
    }

    /// <summary>
    /// Marks a gallery image as primary, demoting any previous one.
    /// </summary>
    /// <exception cref="ProductImageNotOwnedException"><paramref name="imageId"/> is not in this product's gallery.</exception>
    public void SetPrimaryImage(Guid imageId)
    {
        var target = _images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new ProductImageNotOwnedException();

        foreach (var image in _images)
            image.ClearPrimary();

        target.MakePrimary();
    }

    /// <summary>
    /// Removes a gallery image, unpinning any variant that was pinned to it (RULE-13) and
    /// re-asserting RULE-7's "exactly one primary" if the removed image was primary.
    /// </summary>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
            return;

        _images.Remove(image);

        foreach (var variant in _variants.Where(v => v.PinnedImageId == imageId))
            variant.Unpin();

        EnsureExactlyOnePrimary();
    }

    /// <summary>
    /// Replaces the product's option types wholesale — existing types/values kept by id, new
    /// ones with <c>Id: null</c>. Must be followed by <see cref="ApplyVariantConfiguration"/> to
    /// rebuild the variant set against the new option types (Flow 4/6).
    /// </summary>
    /// <exception cref="OptionTypeNameRequiredException">A type's trimmed name is empty.</exception>
    /// <exception cref="OptionTypeValueRequiredException">A type has no values, or one is empty.</exception>
    /// <exception cref="DuplicateOptionNameException">
    /// Two option types are the same, ignoring case and surrounding whitespace.
    /// </exception>
    /// <exception cref="DuplicateOptionValueException">
    /// Two values within one option type are the same, ignoring case and surrounding whitespace.
    /// </exception>
    public void SetOptionTypes(IReadOnlyList<ProductOptionTypeInput> optionTypes)
    {
        var newTypes = new List<ProductOptionType>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var input in optionTypes)
        {
            var type = ProductOptionType.Create(input.Id, input.Name, position, input.Values);
            var key = type.Name.ToLowerInvariant();
            if (!seenNames.Add(key))
                throw new DuplicateOptionNameException(type.Name);

            newTypes.Add(type);
            position++;
        }

        _optionTypes.Clear();
        _optionTypes.AddRange(newTypes);
    }

    /// <summary>
    /// Replaces the product's specification rows wholesale — existing rows kept by id, new ones
    /// with <c>Id: null</c>. Rows keep the order they are given in; nothing rearranges them —
    /// a row's <see cref="ProductSpecification.Position"/> is simply its index.
    /// </summary>
    /// <exception cref="SpecificationNameRequiredException">A row's trimmed name is empty.</exception>
    /// <exception cref="SpecificationValueRequiredException">A row's trimmed value is empty.</exception>
    /// <exception cref="DuplicateSpecificationNameException">
    /// Two rows are the same name, ignoring case and surrounding whitespace.
    /// </exception>
    public void SetSpecifications(IReadOnlyList<ProductSpecificationInput> specifications)
    {
        var newSpecifications = new List<ProductSpecification>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var input in specifications)
        {
            var specification = ProductSpecification.Create(input.Id, input.Name, input.Value, position);
            var key = specification.Name.ToLowerInvariant();
            if (!seenNames.Add(key))
                throw new DuplicateSpecificationNameException(specification.Name);

            newSpecifications.Add(specification);
            position++;
        }

        _specifications.Clear();
        _specifications.AddRange(newSpecifications);
    }

    /// <summary>
    /// Re-derives the variant set as the cartesian product of the product's current option
    /// values (RULE-10) — the ground truth, regardless of what <paramref name="requested"/>
    /// contains. A combination present in <paramref name="requested"/> takes that data; one
    /// absent from it but already configured keeps its prior configuration verbatim (RULE-12); a
    /// brand-new combination starts with a suggested SKU and no price/stock. Rows in
    /// <paramref name="requested"/> whose combination is no longer valid are dropped.
    /// </summary>
    public void ApplyVariantConfiguration(IReadOnlyList<ProductVariantInput> requested)
    {
        if (_optionTypes.Count == 0)
        {
            _variants.Clear();
            return;
        }

        var validCombinations = CartesianProductOfOptionValueIds();
        var existingByKey = _variants.ToDictionary(v => Key(v.OptionValueIds));
        var requestedByKey = requested
            .Where(r => validCombinations.Any(c => Key(c) == Key(r.OptionValueIds)))
            .ToDictionary(r => Key(r.OptionValueIds));

        var newVariants = new List<ProductVariant>();
        var position = 0;
        foreach (var combination in validCombinations)
        {
            var key = Key(combination);
            ProductVariant variant;
            if (requestedByKey.TryGetValue(key, out var input))
            {
                var id = input.Id ?? (existingByKey.TryGetValue(key, out var matched) ? matched.Id : (Guid?)null);
                variant = ProductVariant.Create(
                    id, input.Sku, input.Pricing, input.IsAvailable,
                    input.PinnedImageId, combination, position);
            }
            else if (existingByKey.TryGetValue(key, out var existing))
            {
                variant = ProductVariant.Create(
                    existing.Id, existing.Sku, existing.Pricing, existing.IsAvailable,
                    existing.PinnedImageId, combination, position);
            }
            else
            {
                variant = ProductVariant.Create(
                    null, Sku.Suggest(Sku, LabelsFor(combination)), null, true, null, combination, position);
            }

            newVariants.Add(variant);
            position++;
        }

        _variants.Clear();
        _variants.AddRange(newVariants);
    }

    /// <summary>
    /// Pins (or, with <paramref name="imageId"/> <c>null</c>, unpins) one variant's gallery
    /// image. <paramref name="scope"/> is informational only — the "apply to every variant
    /// sharing this option value" fan-out already happened in the Web layer before this call
    /// (Decision 2), so each call always resolves exactly the one named variant.
    /// </summary>
    /// <exception cref="ProductImageNotOwnedException"><paramref name="imageId"/> is not in this product's gallery.</exception>
    public void PinVariantImage(Guid variantId, Guid? imageId, PinScope scope = PinScope.ThisVariantOnly)
    {
        _ = scope;

        if (imageId is Guid id && _images.All(i => i.Id != id))
            throw new ProductImageNotOwnedException();

        var variant = _variants.FirstOrDefault(v => v.Id == variantId);
        if (variant is null)
            return;

        if (imageId is Guid pinnedId)
            variant.Pin(pinnedId);
        else
            variant.Unpin();
    }

    /// <summary>
    /// Verifies the product has everything a customer-facing listing needs (RULE-14): its own
    /// price when it has no variants, or a price on every variant when it does.
    /// </summary>
    /// <exception cref="ProductNotPublishableException">A required value is missing.</exception>
    public void EnsurePublishable()
    {
        var missing = new List<string>();

        if (!HasVariants)
        {
            if (Pricing is null)
                missing.Add(ProductNotPublishableException.PriceToken);
        }
        else
        {
            foreach (var variant in _variants)
            {
                if (variant.Pricing is null)
                    missing.Add($"{ProductNotPublishableException.VariantPriceTokenPrefix}{variant.Id}");
            }
        }

        if (missing.Count > 0)
            throw new ProductNotPublishableException(missing);
    }

    private void EnsureExactlyOnePrimary()
    {
        if (_images.Count == 0)
            return;

        if (_images.Count(i => i.IsPrimary) == 1)
            return;

        foreach (var image in _images)
            image.ClearPrimary();

        _images[0].MakePrimary();
    }

    private IReadOnlyList<string> LabelsFor(IReadOnlySet<Guid> combination)
    {
        var labels = new List<string>();
        foreach (var type in _optionTypes)
        {
            var value = type.Values.FirstOrDefault(v => combination.Contains(v.Id));
            if (value is not null)
                labels.Add(value.Value);
        }

        return labels;
    }

    private List<IReadOnlySet<Guid>> CartesianProductOfOptionValueIds()
    {
        IEnumerable<IReadOnlySet<Guid>> combinations = [new HashSet<Guid>()];
        foreach (var type in _optionTypes)
        {
            combinations = combinations.SelectMany(existing =>
                type.Values.Select(value =>
                {
                    var next = new HashSet<Guid>(existing) { value.Id };
                    return (IReadOnlySet<Guid>)next;
                }));
        }

        return [.. combinations];
    }

    private static string Key(IReadOnlySet<Guid> ids) => string.Join(",", ids.OrderBy(id => id));

    private static string ValidateName(string name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new ProductNameRequiredException();

        if (trimmedName.Length > MaxNameLength)
            throw new ProductNameTooLongException();

        return trimmedName;
    }
}
