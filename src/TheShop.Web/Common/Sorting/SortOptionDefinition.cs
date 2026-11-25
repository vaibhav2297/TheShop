namespace TheShop.Web.Common.Sorting;

/// <summary>
/// One selectable sort order, defined in the single place a feature declares its sort configuration:
/// the feature's own enum member, the stable URL slug that stands in for it in a shared link, and
/// the resource key its picker label is resolved from.
/// </summary>
/// <remarks>
/// The label is held as a <em>key</em>, not as resolved text, so it is looked up through
/// <c>IStringLocalizer</c> at render time. A catalogue is a static, process-lifetime object; storing
/// the resolved string would freeze the picker in whichever language happened to be active when the
/// type was first touched.
/// </remarks>
/// <typeparam name="TSort">The feature's sort-option enum.</typeparam>
/// <param name="Value">The enum member this option selects.</param>
/// <param name="Slug">The stable URL slug, from <see cref="SortSlugs"/>.</param>
/// <param name="LabelKey">
/// The picker label's resource key, as <c>nameof(Strings.{Key})</c> — a runtime key resolved via
/// <c>Localizer[...]</c>, the same way <c>FilterGroupDto.LabelKey</c> is.
/// </param>
public sealed record SortOptionDefinition<TSort>(TSort Value, string Slug, string LabelKey)
    where TSort : struct, Enum;
