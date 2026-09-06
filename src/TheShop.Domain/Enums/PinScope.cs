namespace TheShop.Domain.Enums;

/// <summary>
/// How far a variant image pin choice reaches (FR-16). The fan-out for
/// <see cref="AllSharingOptionValue"/> happens in the Web layer, which issues one
/// <c>Product.PinVariantImage</c> call per affected variant (Decision 2) — by the time a call
/// reaches the aggregate it always applies to exactly the one named variant.
/// </summary>
public enum PinScope
{
    ThisVariantOnly,
    AllSharingOptionValue,
}
