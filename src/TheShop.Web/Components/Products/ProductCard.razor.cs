using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products.DTOs;

namespace TheShop.Web.Components.Products;

/// <summary>
/// A single product tile on the catalogue grid. Renders the product image (with an
/// icon placeholder fallback), name, and price (sale + struck original when discounted).
/// The Add-to-Cart / Wishlist actions and the card-body click are currently empty
/// callbacks — wired up by the Cart, Wishlist, and product-detail features respectively.
/// </summary>
public partial class ProductCard : MudComponentBase
{

    #region Parameters

    /// <summary>The product to render.</summary>
    [Parameter, EditorRequired]
    public ProductSummaryDto Product { get; set; } = default!;

    /// <summary>
    /// Raised when the Add-to-Cart button is activated. Empty until the Cart feature wires it.
    /// </summary>
    [Parameter]
    public EventCallback OnAddToCart { get; set; }

    /// <summary>
    /// Raised when the Wishlist button is activated. Empty until the Wishlist feature wires it.
    /// </summary>
    [Parameter]
    public EventCallback OnToggleWishlist { get; set; }

    /// <summary>
    /// Raised when the card body is selected. Empty until the product-detail feature wires it.
    /// </summary>
    [Parameter]
    public EventCallback OnSelect { get; set; }

    #endregion

    /// <summary>
    /// Returns the CSS class for the product card
    /// </summary>
    private string ClassName =>
        new CssBuilder("shop-product-tile")
            .AddClass("white")
            .AddClass(Class)
            .Build();

    private Task OnAddToCartAsync() => OnAddToCart.InvokeAsync();

    private Task OnToggleWishlistAsync() => OnToggleWishlist.InvokeAsync();

    private Task OnSelectAsync() => OnSelect.InvokeAsync();
}
