using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>
/// One value of a <see cref="ProductOptionType"/> (e.g. "Mango" under a "Flavour" type). Owned
/// by its option type — never persisted or mutated on its own.
/// </summary>
public sealed class ProductOptionValue
{
    public Guid Id { get; }
    public string Value { get; }
    public int Position { get; }

    private ProductOptionValue(Guid id, string value, int position)
    {
        Id = id;
        Value = value;
        Position = position;
    }

    /// <summary>
    /// Creates a <see cref="ProductOptionValue"/>, reusing <paramref name="id"/> when supplied
    /// (an existing value kept across an edit) or assigning a new one otherwise.
    /// </summary>
    /// <exception cref="OptionTypeValueRequiredException">The trimmed value is empty.</exception>
    public static ProductOptionValue Create(Guid? id, string value, int position)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new OptionTypeValueRequiredException();

        return new ProductOptionValue(id ?? Guid.NewGuid(), trimmed, position);
    }
}
