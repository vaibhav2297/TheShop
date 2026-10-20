using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Common;

/// <summary>Non-interactive Figma label badge; use ShopChip for selectable content.</summary>
public partial class ShopBadge : ShopComponentBase
{
    /// <summary>Localized non-interactive label content.</summary>
    [Parameter, EditorRequired] public RenderFragment? ChildContent { get; set; }

    /// <summary>Filled or Outlined treatment. Text is not a badge variant.</summary>
    [Parameter] public ShopVariant Variant { get; set; } = ShopVariant.Filled;

    /// <summary>Primary, Secondary, Info, Success, Warning or Error; other roles are not in this design.</summary>
    [Parameter] public ShopColor Color { get; set; } = ShopColor.Primary;

    /// <summary>Small, Medium or Large geometry; defaults to Medium.</summary>
    [Parameter] public ShopSize Size { get; set; } = ShopSize.Medium;

    private string ClassName => ShopCssClass.Join("shop-native", "shop-badge",
        ShopCssClass.Modifier("shop-badge", Variant), ShopCssClass.Modifier("shop-badge", Color),
        ShopCssClass.Modifier("shop-badge", Size), Class);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (Variant is not (ShopVariant.Filled or ShopVariant.Outlined))
            throw new ArgumentOutOfRangeException(nameof(Variant));
        if (Color is not (ShopColor.Primary or ShopColor.Secondary or ShopColor.Info or ShopColor.Success or ShopColor.Warning or ShopColor.Error))
            throw new ArgumentOutOfRangeException(nameof(Color));
    }
}
