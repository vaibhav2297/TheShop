namespace TheShop.Web.Components.Products;

/// <summary>Staged ordered images and the exact variants to receive them on Apply.</summary>
public sealed record VariantImageSelectionResult(
    IReadOnlyList<Guid> ImageIds, IReadOnlyList<Guid> TargetVariantIds, bool Append);
