namespace TheShop.Web.Components.Products;

/// <summary>
/// The staff member's choice from <see cref="VariantImageDialog"/>: the gallery image to pin
/// (<c>null</c> clears the pin) and whether to fan it out to every variant sharing the dialog's
/// shared-option-value scope (FR-16). The fan-out itself happens in the caller
/// (<c>ProductVariantsCard</c>) before any command is built (Decision 2).
/// </summary>
public sealed record VariantImagePinResult(Guid? ImageId, bool ApplyToAllSharing);
