using TheShop.Web.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Pages.Admin;

/// <summary>Editable draft with prefilled section fields and no categories.</summary>
public sealed class HomepageCategoryFormModel
{
    /// <summary>Section title.</summary>
    public string Title { get; set; } = Strings.HomepageCategory_DefaultTitle;
    /// <summary>Button text.</summary>
    public string ButtonLabel { get; set; } = Strings.HomepageCategory_ViewAllCategories;
    /// <summary>Button destination.</summary>
    public string ButtonLink { get; set; } = Routes.Categories;
    /// <summary>Selected IDs in display order.</summary>
    public List<Guid> CategoryIds { get; set; } = [];
    /// <summary>Visibility choice.</summary>
    public bool IsVisible { get; set; } = true;
}
