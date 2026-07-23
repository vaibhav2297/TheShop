using System.Text.RegularExpressions;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>
/// A product brand. Reference data used to organize and filter the catalogue, and a
/// staff-managed aggregate whose name/description length and logo are enforced here.
/// </summary>
public sealed partial class Brand
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    public Guid Id { get; }
    public string Name { get; }
    public string Slug { get; }
    public string? Description { get; private set; }
    public string? LogoPath { get; private set; }
    public bool IsActive { get; private set; }

    private Brand(Guid id, string name, string slug, string? description, string? logoPath, bool isActive)
    {
        Id = id;
        Name = name;
        Slug = slug;
        Description = description;
        LogoPath = logoPath;
        IsActive = isActive;
    }

    /// <summary>
    /// Creates a new <see cref="Brand"/>, enforcing the name/description invariants
    /// (RULE-1/RULE-3) and generating its <see cref="Slug"/> from the trimmed name.
    /// </summary>
    /// <exception cref="BrandNameRequiredException">The name is empty or whitespace-only.</exception>
    /// <exception cref="BrandNameTooLongException">The trimmed name exceeds 100 characters.</exception>
    /// <exception cref="BrandDescriptionTooLongException">The trimmed description exceeds 250 characters.</exception>
    public static Brand Create(string name, string? description, bool isActive)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new BrandNameRequiredException();

        if (trimmedName.Length > MaxNameLength)
            throw new BrandNameTooLongException();

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > MaxDescriptionLength })
            throw new BrandDescriptionTooLongException();

        return new Brand(Guid.NewGuid(), trimmedName, GenerateSlug(trimmedName), trimmedDescription, logoPath: null, isActive);
    }

    /// <summary>
    /// Rehydrates a <see cref="Brand"/> from persisted data. Invariants are not re-checked —
    /// persisted rows are already valid.
    /// </summary>
    public static Brand Rehydrate(
        Guid id,
        string name,
        string slug,
        string? description = null,
        string? logoPath = null,
        bool isActive = true) =>
        new(id, name, slug, description, logoPath, isActive);

    /// <summary>
    /// Attaches an uploaded logo's storage key after a successful upload.
    /// </summary>
    public void AttachLogo(string logoPath) => LogoPath = logoPath;

    private static string GenerateSlug(string trimmedName)
    {
        var hyphenated = NonAlphanumericRun().Replace(trimmedName.ToLowerInvariant(), "-");
        return hyphenated.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRun();
}
