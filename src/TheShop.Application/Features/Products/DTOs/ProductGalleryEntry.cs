namespace TheShop.Application.Features.Products.DTOs;

/// <summary>
/// One entry in a create/update command's desired gallery state: an existing image kept in
/// place (<see cref="ImageId"/> set) or a new upload (<see cref="Upload"/> set). Exactly one of
/// the two is populated. <see cref="Position"/> is the entry's place in the gallery's display
/// order; nothing is uploaded or persisted until the command is handled (Decision 9).
/// </summary>
/// <param name="ImageId">The stored image's identifier when this entry keeps one, otherwise <c>null</c>.</param>
/// <param name="Upload">The file's bytes and metadata when this entry is a new image, otherwise <c>null</c>.</param>
/// <param name="Position">The entry's place in the gallery's display order; position 0 is the primary image.</param>
/// <param name="IsPrimary">Whether this entry is the product's primary image (RULE-7).</param>
/// <param name="ClientId">
/// The caller's own identifier for this entry, used solely to correlate a variant's
/// <c>PinnedImageId</c> with an image that does not have a real identifier yet — a staff member
/// can pin a just-picked image before the product is ever saved. It equals
/// <paramref name="ImageId"/> for a kept image, and is a client-minted correlation id for a new
/// upload; the handler resolves it to the identifier the domain mints, so a client-supplied value
/// never becomes an image's identity.
/// </param>
public sealed record ProductGalleryEntry(
    Guid? ImageId, ProductImageUpload? Upload, int Position, bool IsPrimary, Guid ClientId);
