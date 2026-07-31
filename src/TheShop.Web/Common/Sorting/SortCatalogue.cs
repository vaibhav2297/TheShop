namespace TheShop.Web.Common.Sorting;

/// <summary>
/// The complete sort configuration of one feature's list: which orders it offers, the order the
/// picker shows them in, which one an empty URL resolves to, and the stable slug each is written as.
/// Derive one sealed catalogue per feature and declare every option in its constructor — adding an
/// order is then a single line, and the picker, the URL slug, and the default all follow from it.
/// </summary>
/// <remarks>
/// Sort enums stay per-feature (<c>BrandSortOption</c>, <c>ProductSortOption</c>) rather than
/// collapsing into one shared enum, so a brand list cannot be asked to sort by price: the catalogue
/// shares the *vocabulary* between features, never the type.
/// </remarks>
/// <typeparam name="TSort">The feature's sort-option enum.</typeparam>
public abstract class SortCatalogue<TSort> where TSort : struct, Enum
{
    private readonly Dictionary<TSort, string> _slugByValue;
    private readonly Dictionary<string, TSort> _valueBySlug;

    /// <summary>Declares a feature's sort options.</summary>
    /// <param name="defaultSort">
    /// The order an absent or unrecognised slug resolves to. Must be one of <paramref name="options"/>.
    /// </param>
    /// <param name="options">Every offered option, in the order the picker should display them.</param>
    /// <exception cref="ArgumentException">
    /// No options were given, two options share a value or a slug, or the default is not among them.
    /// </exception>
    protected SortCatalogue(TSort defaultSort, params SortOptionDefinition<TSort>[] options)
    {
        if (options.Length == 0)
            throw new ArgumentException("A sort catalogue must offer at least one option.", nameof(options));

        _slugByValue = new Dictionary<TSort, string>(options.Length);
        _valueBySlug = new Dictionary<string, TSort>(options.Length, StringComparer.Ordinal);

        // Add throws on a duplicate key, which is the point: one enum member registered twice, or two
        // members sharing a slug, makes the URL round-trip lossy. Failing at type init turns that into
        // an immediate, obvious break rather than links that quietly resolve to the wrong order.
        foreach (var option in options)
        {
            _slugByValue.Add(option.Value, option.Slug);
            _valueBySlug.Add(option.Slug, option.Value);
        }

        if (!_slugByValue.ContainsKey(defaultSort))
        {
            throw new ArgumentException(
                "The default sort order must be one of the offered options.", nameof(defaultSort));
        }

        Default = defaultSort;
        Options = options;
        Picker = [.. options.Select(option => (option.Value, option.LabelKey))];
    }

    /// <summary>
    /// The order an absent or unrecognised <c>sort</c> slug resolves to. Omitted when writing the
    /// URL, so a default view produces a clean link.
    /// </summary>
    public TSort Default { get; }

    /// <summary>Every offered option, in picker display order.</summary>
    public IReadOnlyList<SortOptionDefinition<TSort>> Options { get; }

    /// <summary>
    /// The value/label-key pairs that feed <c>ShopSortSelect.Options</c>. Labels are keys, resolved
    /// through the localizer at render — see <see cref="SortOptionDefinition{TSort}"/>.
    /// </summary>
    public IReadOnlyList<(TSort Value, string LabelKey)> Picker { get; }

    /// <summary>
    /// Returns the stable URL slug for <paramref name="sort"/>, falling back to the
    /// <see cref="Default"/>'s slug for a value this catalogue does not offer.
    /// </summary>
    public string ToSlug(TSort sort) =>
        _slugByValue.TryGetValue(sort, out var slug) ? slug : _slugByValue[Default];

    /// <summary>
    /// Resolves a URL slug back to a sort order, falling back to <see cref="Default"/> for an
    /// unknown or <c>null</c> slug — a hand-edited or stale link degrades to the default view rather
    /// than erroring.
    /// </summary>
    public TSort FromSlug(string? slug) =>
        slug is not null && _valueBySlug.TryGetValue(slug, out var value) ? value : Default;
}
