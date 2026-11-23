using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>Ordered unique images for one variant; the first image is its cover.</summary>
public sealed class VariantImageAssignment
{
    /// <summary>Image identifiers in display order, independent of the product primary image.</summary>
    public IReadOnlyList<Guid> ImageIds { get; }

    /// <summary>Copies an ordered assignment, rejecting duplicate identifiers.</summary>
    /// <exception cref="DuplicateVariantImageException">An image appears more than once.</exception>
    public VariantImageAssignment(IReadOnlyList<Guid> imageIds)
    {
        if (imageIds.Distinct().Count() != imageIds.Count)
            throw new DuplicateVariantImageException();

        ImageIds = Array.AsReadOnly(imageIds.ToArray());
    }

    /// <summary>Appends missing images in supplied order, preserving the existing cover and order.</summary>
    public VariantImageAssignment AppendMissing(IReadOnlyList<Guid> imageIds) =>
        new([.. ImageIds.Concat(imageIds).Distinct()]);

    /// <summary>Removes assigned images while retaining the order of the remainder.</summary>
    public VariantImageAssignment Remove(IReadOnlyList<Guid> imageIds)
    {
        var removed = imageIds.ToHashSet();
        return new([.. ImageIds.Where(id => !removed.Contains(id))]);
    }
}
