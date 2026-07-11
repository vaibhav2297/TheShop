namespace TheShop.Domain.Entities;

/// <summary>
/// A product category. Reference data used to organize and filter the catalogue.
/// </summary>
public sealed class Category
{
    public Guid Id { get; }
    public string Name { get; }
    public string Slug { get; }

    private Category(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }

    /// <summary>
    /// Creates a <see cref="Category"/> from persisted data. Reference data has no invariants to enforce.
    /// </summary>
    public static Category Create(Guid id, string name, string slug) => new(id, name, slug);
}
