using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Admin;

/// <summary>Editable section fields with no brands selected for Add.</summary>
public sealed class HomepageBrandFormModel
{
    /// <summary>Section title.</summary>
    public string Title { get; set; } = Strings.HomepageBrand_DefaultTitle;
    /// <summary>Button text.</summary>
    public string ButtonLabel { get; set; } = Strings.HomepageBrand_ViewAllBrands;
    /// <summary>Button destination.</summary>
    public string ButtonLink { get; set; } = Routes.Brands;
    /// <summary>Selected IDs in display order.</summary>
    public List<Guid> BrandIds { get; set; } = [];
    /// <summary>Visibility choice.</summary>
    public bool IsVisible { get; set; } = true;
}
