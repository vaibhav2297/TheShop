using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Domain.Entities;

/// <summary>
/// One option type a product varies by (e.g. "Flavour", "Colour"). Owned by
/// <see cref="Product"/> — never persisted or mutated on its own. Enforces that its own name is
/// present and that its values are present and unique within itself (RULE-9); uniqueness of the
/// type's name against the product's other option types is enforced by
/// <see cref="Product.SetOptionTypes"/>.
/// </summary>
public sealed class ProductOptionType
{
    private readonly List<ProductOptionValue> _values = [];

    public Guid Id { get; }
    public string Name { get; }
    public int Position { get; }
    public IReadOnlyList<ProductOptionValue> Values => _values.AsReadOnly();

    private ProductOptionType(Guid id, string name, int position)
    {
        Id = id;
        Name = name;
        Position = position;
    }

    /// <summary>
    /// Creates a <see cref="ProductOptionType"/>, reusing <paramref name="id"/> when supplied
    /// (an existing type kept across an edit) or assigning a new one otherwise.
    /// </summary>
    /// <exception cref="OptionTypeNameRequiredException">The trimmed name is empty.</exception>
    /// <exception cref="OptionTypeValueRequiredException"><paramref name="values"/> is empty.</exception>
    /// <exception cref="DuplicateOptionValueException">
    /// Two values are the same, ignoring case and surrounding whitespace.
    /// </exception>
    public static ProductOptionType Create(
        Guid? id, string name, int position, IReadOnlyList<ProductOptionValueInput> values)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new OptionTypeNameRequiredException();

        if (values.Count == 0)
            throw new OptionTypeValueRequiredException();

        var type = new ProductOptionType(id ?? Guid.NewGuid(), trimmedName, position);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var valuePosition = 0;
        foreach (var input in values)
        {
            var optionValue = ProductOptionValue.Create(input.Id, input.Value, valuePosition++);
            var key = optionValue.Value.ToLowerInvariant();
            if (!seen.Add(key))
                throw new DuplicateOptionValueException(optionValue.Value);

            type._values.Add(optionValue);
        }

        return type;
    }
}
