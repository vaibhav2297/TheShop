namespace TheShop.Web.Components.Brands;

/// <summary>Editable values shared by create and edit brand forms.</summary>
public sealed class BrandFormModel
{
    /// <summary>Brand name entered by the user.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional brand description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether the brand is active.</summary>
    public bool IsActive { get; set; } = true;
}
