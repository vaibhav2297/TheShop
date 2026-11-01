using TheShop.Domain.Enums;
using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Admin;

/// <summary>Editable Fresh Drops draft, separate from saved configuration.</summary>
public sealed class FreshDropsFormModel
{
    /// <summary>Section heading text.</summary>
    public string Title { get; set; } = Strings.Homepage_FreshDrops;
    /// <summary>Optional button text.</summary>
    public string? ButtonLabel { get; set; } = Strings.FreshDrops_ViewAllProducts;
    /// <summary>Optional button destination.</summary>
    public string? ButtonLink { get; set; } = Routes.Products;
    /// <summary>How products are selected.</summary>
    public FreshDropsSelectionMode SelectionMode { get; set; } = FreshDropsSelectionMode.Newest;
    /// <summary>Draft manual order; retained during unsaved mode switches.</summary>
    public List<Guid> ManualProductIds { get; set; } = [];
    /// <summary>Visibility choice.</summary>
    public bool IsVisible { get; set; } = true;
}
