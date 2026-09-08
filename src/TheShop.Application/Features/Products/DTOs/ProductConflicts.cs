namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// The outcome of checking a product save's name and SKUs against the catalogue's uniqueness
/// rules (RULE-2, RULE-8). <see cref="SkusTaken"/> lists every submitted SKU — the product's own
/// or a variant's — that already belongs to a different product or variant.
/// </summary>
public sealed record ProductConflicts(bool NameTaken, IReadOnlyList<string> SkusTaken)
{
    public bool HasConflicts => NameTaken || SkusTaken.Count > 0;

    public static ProductConflicts None { get; } = new(false, []);
}
