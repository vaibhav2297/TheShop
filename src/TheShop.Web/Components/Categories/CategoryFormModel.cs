namespace TheShop.Web.Components.Categories;

/// <summary>Editable values shared by create and edit category forms.</summary>
public sealed class CategoryFormModel
{
    /// <summary>Category name entered by the user.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional category description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the category is active.</summary>
    public bool IsActive { get; set; } = true;
}
