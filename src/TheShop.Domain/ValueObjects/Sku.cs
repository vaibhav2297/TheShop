using System.Text.RegularExpressions;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// A product or variant SKU. Every SKU in the catalogue — product-level and variant-level —
/// shares one store-wide uniqueness namespace, compared on <see cref="Normalized"/>.
/// </summary>
public sealed class Sku : IEquatable<Sku>
{
    public string Value { get; }

    /// <summary>
    /// The comparison key: lowercased, trimmed. Two SKUs differing only by case or surrounding
    /// whitespace collide (RULE-8).
    /// </summary>
    public string Normalized => Value.ToLowerInvariant();

    private Sku(string value) => Value = value;

    /// <summary>
    /// Creates a <see cref="Sku"/> from staff-entered text, trimming surrounding whitespace.
    /// </summary>
    /// <exception cref="SkuRequiredException">The trimmed value is empty.</exception>
    public static Sku Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new SkuRequiredException();

        return new Sku(trimmed);
    }

    /// <summary>
    /// Suggests a product-level SKU derived from the product's name (Decision 7): an
    /// uppercased, hyphenated slug of the name.
    /// </summary>
    public static Sku Suggest(string productName)
    {
        var slug = Slugify(productName);
        return new Sku(slug.Length == 0 ? "SKU" : slug);
    }

    /// <summary>
    /// Suggests a variant SKU derived from the owning product's SKU and the variant's option
    /// values: <c>{ProductSku}-{Value}-{Value}</c> (FR-15).
    /// </summary>
    public static Sku Suggest(Sku productSku, IReadOnlyList<string> optionValues)
    {
        var suffix = string.Join("-", optionValues.Select(Slugify).Where(part => part.Length > 0));
        var candidate = suffix.Length == 0 ? productSku.Value : $"{productSku.Value}-{suffix}";
        return new Sku(candidate);
    }

    private static string Slugify(string input)
    {
        var trimmed = input?.Trim() ?? string.Empty;
        var slug = Regex.Replace(trimmed, "[^A-Za-z0-9]+", "-").Trim('-');
        return slug.ToUpperInvariant();
    }

    public bool Equals(Sku? other) =>
        other is not null && string.Equals(Normalized, other.Normalized, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Sku);

    public override int GetHashCode() => Normalized.GetHashCode();

    public override string ToString() => Value;
}
