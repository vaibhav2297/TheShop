using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The variant editor's current state, as raised to the product form: the two command inputs plus
/// the labels the form needs to talk about a variant in a message it shows the staff member. The
/// labels travel alongside rather than inside <see cref="VariantInput"/> because they are a display
/// concern the command has no use for.
/// </summary>
/// <param name="OptionTypes">The option types as the command carries them.</param>
/// <param name="Variants">The generated variant rows as the command carries them.</param>
/// <param name="LabelsByVariantId">Each variant's human-readable label (e.g. <c>Mango / 20mg</c>), keyed by its identifier.</param>
public sealed record ProductVariantsState(
    IReadOnlyList<OptionTypeInput> OptionTypes,
    IReadOnlyList<VariantInput> Variants,
    IReadOnlyDictionary<Guid, string> LabelsByVariantId);
