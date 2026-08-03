using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>
/// A product category. Reference data used to organize and filter the catalogue, and a
/// staff-managed aggregate whose name/description length and image are enforced here.
/// </summary>
public sealed class Category
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 250;

    public Guid Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string? ImagePath { get; private set; }
    public bool IsActive { get; private set; }

    private Category(Guid id, string name, string? description, string? imagePath, bool isActive)
    {
        Id = id;
        Name = name;
        Description = description;
        ImagePath = imagePath;
        IsActive = isActive;
    }

    /// <summary>
    /// Creates a new <see cref="Category"/>, enforcing the name/description invariants
    /// (RULE-1/RULE-3).
    /// </summary>
    /// <exception cref="CategoryNameRequiredException">The name is empty or whitespace-only.</exception>
    /// <exception cref="CategoryNameTooLongException">The trimmed name exceeds 100 characters.</exception>
    /// <exception cref="CategoryDescriptionTooLongException">The trimmed description exceeds 250 characters.</exception>
    public static Category Create(string name, string? description, bool isActive)
    {
        var trimmedName = ValidateName(name);
        var trimmedDescription = ValidateDescription(description);

        return new Category(Guid.NewGuid(), trimmedName, trimmedDescription, imagePath: null, isActive);
    }

    /// <summary>
    /// Rehydrates a <see cref="Category"/> from persisted data. Invariants are not re-checked —
    /// persisted rows are already valid.
    /// </summary>
    public static Category Rehydrate(
        Guid id,
        string name,
        string? description = null,
        string? imagePath = null,
        bool isActive = true) =>
        new(id, name, description, imagePath, isActive);

    /// <summary>
    /// Renames the category, re-validating the same invariants <see cref="Create"/> enforces.
    /// </summary>
    /// <exception cref="CategoryNameRequiredException">The name is empty or whitespace-only.</exception>
    /// <exception cref="CategoryNameTooLongException">The trimmed name exceeds 100 characters.</exception>
    public void Rename(string name) => Name = ValidateName(name);

    /// <summary>
    /// Changes the category's description, re-validating the same invariant <see cref="Create"/> enforces.
    /// </summary>
    /// <exception cref="CategoryDescriptionTooLongException">The trimmed description exceeds 250 characters.</exception>
    public void ChangeDescription(string? description) => Description = ValidateDescription(description);

    /// <summary>
    /// Activates the category, restoring it to the customer-facing filter facet.
    /// </summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Deactivates the category, hiding it from the customer-facing filter facet without
    /// affecting the products assigned to it.
    /// </summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Attaches an uploaded image's storage key after a successful upload.
    /// </summary>
    public void AttachImage(string imagePath) => ImagePath = imagePath;

    /// <summary>
    /// Clears the image, returning the previous storage key (if any) so the caller can dispose
    /// the underlying object.
    /// </summary>
    public string? RemoveImage()
    {
        var previousPath = ImagePath;
        ImagePath = null;
        return previousPath;
    }

    private static string ValidateName(string name)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
            throw new CategoryNameRequiredException();

        if (trimmedName.Length > MaxNameLength)
            throw new CategoryNameTooLongException();

        return trimmedName;
    }

    private static string? ValidateDescription(string? description)
    {
        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is { Length: > MaxDescriptionLength })
            throw new CategoryDescriptionTooLongException();

        return trimmedDescription;
    }
}
