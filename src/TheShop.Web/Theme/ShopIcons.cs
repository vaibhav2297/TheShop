namespace TheShop.Web.Theme;

/// <summary>
/// Registry of custom SVG icon paths used throughout the shop.
/// All icons use semantic names (Cart, not ShoppingBag) so the visual
/// representation can be swapped without renaming call-sites.
///
/// To add an icon:
///   1. Export the SVG from Figma
///   2. Extract the &lt;path d="..."/&gt; markup
///   3. Add a new constant below with a semantic name
/// </summary>
public static class ShopIcons
{
    // TODO: Replace placeholder strings with actual SVG path data from Figma.

    // Navigation
    public const string Home    = ""; // TODO: SVG path
    public const string Menu    = ""; // TODO: SVG path
    public const string Close   = ""; // TODO: SVG path
    public const string Back    = ""; // TODO: SVG path

    // Commerce
    public const string Cart    = ""; // TODO: SVG path
    public const string CartAdd = ""; // TODO: SVG path
    public const string Wishlist = ""; // TODO: SVG path
    public const string Checkout = ""; // TODO: SVG path

    // Account
    public const string Login   = ""; // TODO: SVG path
    public const string Logout  = ""; // TODO: SVG path
    public const string Account = ""; // TODO: SVG path

    // Utility
    public const string Search  = ""; // TODO: SVG path
    public const string Filter  = ""; // TODO: SVG path
    public const string Sort    = ""; // TODO: SVG path
    public const string Share   = ""; // TODO: SVG path
}
