namespace TheShop.Domain.Entities;

/// <summary>
/// One image in a product's gallery. Owned by <see cref="Product"/> — never persisted or
/// mutated on its own.
/// </summary>
public sealed class ProductImage
{
    public Guid Id { get; }
    public string ObjectKey { get; }
    public int Position { get; private set; }
    public bool IsPrimary { get; private set; }

    private ProductImage(Guid id, string objectKey, int position, bool isPrimary)
    {
        Id = id;
        ObjectKey = objectKey;
        Position = position;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Creates a newly uploaded gallery image.
    /// </summary>
    public static ProductImage Create(string objectKey, int position, bool isPrimary) =>
        new(Guid.NewGuid(), objectKey, position, isPrimary);

    /// <summary>
    /// Rehydrates an existing gallery image, preserving its identity.
    /// </summary>
    public static ProductImage Rehydrate(Guid id, string objectKey, int position, bool isPrimary) =>
        new(id, objectKey, position, isPrimary);

    /// <summary>
    /// Moves the image to a new position in the gallery's display order.
    /// </summary>
    public void MoveTo(int position) => Position = position;

    /// <summary>
    /// Marks this image as the gallery's primary image.
    /// </summary>
    public void MakePrimary() => IsPrimary = true;

    /// <summary>
    /// Clears the primary flag. Called by <see cref="Product"/> to keep RULE-7's "exactly one
    /// primary" invariant while reassigning it elsewhere.
    /// </summary>
    internal void ClearPrimary() => IsPrimary = false;
}
