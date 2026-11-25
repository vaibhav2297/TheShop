namespace TheShop.Domain.ValueObjects;

/// <summary>
/// One entry in a gallery replacement request passed to <c>Product.SetGallery</c>: either an
/// existing image (<paramref name="Id"/> set) kept in place, or a newly uploaded one
/// (<paramref name="Id"/> <c>null</c>). List order is the gallery's display order.
/// </summary>
public sealed record GalleryImageInput(Guid? Id, string ObjectKey, bool IsPrimary);
