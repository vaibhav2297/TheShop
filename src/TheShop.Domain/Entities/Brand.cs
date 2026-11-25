using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>
/// A product brand. Reference data used to organize and filter the catalogue, and a
/// staff-managed aggregate whose name/description length and logo are enforced here.
/// </summary>
public sealed class Brand
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? LogoPath { get; private set; }
    public bool IsActive { get; private set; }

    private Brand(Guid id, string name, string? description, string? logoPath, bool isActive)
    {
        Id = id;
        Name = name;
        Description = description;
        LogoPath = logoPath;
        IsActive = isActive;
    }

    /// <summary>
    /// Creates a new <see cref="Brand"/>, enforcing the name/description invariants
    /// (RULE-1/RULE-3).
    /// </summary>
    /// <exception cref="BrandNameRequiredException">The name is empty or whitespace-only.</exception>
    /// <exception cref="BrandNameTooLongException">The trimmed name exceeds 100 characters.</exception>
    /// <exception cref="BrandDescriptionTooLongException">The trimmed description exceeds 250 characters.</exception>
    public static Brand Create(string name, string? description, bool isActive)
    {
        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);

        return new Brand(Guid.NewGuid(), trimmedName, trimmedDescription, logoPath: null, isActive);
    }

    /// <summary>
    /// Rehydrates a <see cref="Brand"/> from persisted data. Invariants are not re-checked —
    /// persisted rows are already valid.
    /// </summary>
    public static Brand Rehydrate(
        Guid id,
        string name,
        string? description = null,
        string? logoPath = null,
        bool isActive = true) =>
        new(id, name, description, logoPath, isActive);

    /// <summary>
    /// Renames the brand, re-validating the same invariants <see cref="Create"/> enforces.
    /// </summary>
    /// <exception cref="BrandNameRequiredException">The name is empty or whitespace-only.</exception>
    /// <exception cref="BrandNameTooLongException">The trimmed name exceeds 100 characters.</exception>
    public void Rename(string name) => Name = ValidateName(name);

    /// <summary>
    /// Changes the brand's description, re-validating the same invariant <see cref="Create"/> enforces.
    /// </summary>
    /// <exception cref="BrandDescriptionTooLongException">The trimmed description exceeds 250 characters.</exception>
    public void ChangeDescription(string? description) => Description = ValidateDescription(description);

    /// <summary>
    /// Activates the brand, making it visible to customers.
    /// </summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Deactivates the brand, hiding it from customers.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Attaches an uploaded logo's storage key after a successful upload.
    /// </summary>
    public void AttachLogo(string logoPath) => LogoPath = logoPath;

    /// <summary>
    /// Clears the logo, returning the previous storage key (if any) so the caller can dispose
    /// the underlying object.
    /// </summary>
    public string? RemoveLogo()
    {
        var previousPath = LogoPath;
        LogoPath = null;
        return previousPath;
    }

    private static string ValidateName(string name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new BrandNameRequiredException();

        if (trimmedName.Length > MaxNameLength)
            throw new BrandNameTooLongException();

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > MaxDescriptionLength })
            throw new BrandDescriptionTooLongException();

        return trimmedDescription;
    }
}
