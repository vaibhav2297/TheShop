using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// A catalogue product. Owns pricing (via <see cref="ProductPricing"/>) and stock/availability behaviour.
/// </summary>
public sealed class Product
{
    public Guid Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string? ImageUrl { get; }
    public ProductPricing Pricing { get; }
    public int StockQuantity { get; }
    public bool IsPublished { get; }
    public Category Category { get; }
    public Brand Brand { get; }
    public string? Flavour { get; }
    public int? NicotineStrengthMg { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// <c>true</c> when the product currently has a sale price.
    /// </summary>
    public bool IsDiscounted => Pricing.IsDiscounted;

    /// <summary>
    /// The price to charge and display prominently: the sale price when discounted, otherwise the original price.
    /// </summary>
    public Money EffectivePrice => Pricing.Effective;

    /// <summary>
    /// <c>true</c> when the product has at least one unit in stock.
    /// </summary>
    public bool IsInStock => StockQuantity > 0;

    private Product(
        Guid id,
        string name,
        string description,
        string? imageUrl,
        ProductPricing pricing,
        int stockQuantity,
        bool isPublished,
        Category category,
        Brand brand,
        string? flavour,
        int? nicotineStrengthMg,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        ImageUrl = imageUrl;
        Pricing = pricing;
        StockQuantity = stockQuantity;
        IsPublished = isPublished;
        Category = category;
        Brand = brand;
        Flavour = flavour;
        NicotineStrengthMg = nicotineStrengthMg;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a new <see cref="Product"/>, assigning a new identity and creation timestamp.
    /// Invariants (e.g. <c>SalePrice &lt;= OriginalPrice</c>) are enforced by the value objects
    /// (<see cref="ProductPricing"/>) supplied as arguments.
    /// </summary>
    /// <param name="createdAt">Timestamp to assign; defaults to <c>DateTimeOffset.UtcNow</c> when <c>null</c>.</param>
    public static Product Create(
        string name,
        string description,
        string? imageUrl,
        ProductPricing pricing,
        int stockQuantity,
        bool isPublished,
        Category category,
        Brand brand,
        string? flavour,
        int? nicotineStrengthMg,
        DateTimeOffset? createdAt = null)
    {
        return new Product(
            Guid.NewGuid(),
            name,
            description,
            imageUrl,
            pricing,
            stockQuantity,
            isPublished,
            category,
            brand,
            flavour,
            nicotineStrengthMg,
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Reconstructs a <see cref="Product"/> from persisted data without re-running creation invariants.
    /// Use only from repository mappers.
    /// </summary>
    public static Product Rehydrate(
        Guid id,
        string name,
        string description,
        string? imageUrl,
        ProductPricing pricing,
        int stockQuantity,
        bool isPublished,
        Category category,
        Brand brand,
        string? flavour,
        int? nicotineStrengthMg,
        DateTimeOffset createdAt)
    {
        return new Product(
            id,
            name,
            description,
            imageUrl,
            pricing,
            stockQuantity,
            isPublished,
            category,
            brand,
            flavour,
            nicotineStrengthMg,
            createdAt);
    }
}
