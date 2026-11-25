using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>
/// One name/value specification row owned by <see cref="Product"/> — never persisted or
/// mutated on its own. Enforces that its own trimmed name and value are present (RULE-3);
/// uniqueness of the row's name against the product's other rows is enforced by
/// <see cref="Product.SetSpecifications"/>.
/// </summary>
public sealed class ProductSpecification
{
    public Guid Id { get; }
    public string Name { get; }
    public string Value { get; }
    public int Position { get; }

    private ProductSpecification(Guid id, string name, string value, int position)
    {
        Id = id;
        Name = name;
        Value = value;
        Position = position;
    }

    /// <summary>
    /// Creates a <see cref="ProductSpecification"/>, reusing <paramref name="id"/> when supplied
    /// (an existing row kept across an edit) or assigning a new one otherwise.
    /// </summary>
    /// <exception cref="SpecificationNameRequiredException">The trimmed name is empty.</exception>
    /// <exception cref="SpecificationValueRequiredException">The trimmed value is empty.</exception>
    public static ProductSpecification Create(Guid? id, string name, string value, int position)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new SpecificationNameRequiredException();

        var trimmedValue = value?.Trim() ?? string.Empty;
        if (trimmedValue.Length == 0)
            throw new SpecificationValueRequiredException();

        return new ProductSpecification(id ?? Guid.NewGuid(), trimmedName, trimmedValue, position);
    }
}
