namespace TheShop.Domain.Entities;

/// <summary>
/// A product brand. Reference data used to organize and filter the catalogue.
/// </summary>
public sealed class Brand
{
    public Guid Id { get; }
    public string Name { get; }
    public string Slug { get; }

    private Brand(Guid id, string name, string slug)
    {
        Id = id;
        Name = name;
        Slug = slug;
    }

    /// <summary>
    /// Creates a <see cref="Brand"/> from persisted data. Reference data has no invariants to enforce.
    /// </summary>
    public static Brand Create(Guid id, string name, string slug) => new(id, name, slug);
}
