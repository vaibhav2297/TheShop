using TheShop.Web.Resources;

namespace TheShop.Web.Common;

/// <summary>
/// A single footer link. <see cref="Href"/> is <c>null</c> for items whose destination
/// page does not exist yet — those render as inert text and become links the moment an
/// <see cref="Routes"/> entry (or <c>mailto:</c>/<c>tel:</c> target) is supplied here.
/// </summary>
/// <param name="Label">The localized link text.</param>
/// <param name="Href">The navigation target, or <c>null</c> for an inert placeholder.</param>
public sealed record FooterLink(string Label, string? Href = null);

/// <summary>A titled column of footer links.</summary>
/// <param name="Title">The localized section heading.</param>
/// <param name="Links">The links shown under the heading.</param>
public sealed record FooterSection(string Title, IReadOnlyList<FooterLink> Links);

/// <summary>
/// Builds the footer's section/link content. Kept data-driven so links are added,
/// removed, or pointed at real routes by editing this one factory — the
/// <c>ShopFooter</c> component renders whatever it returns. Built per call (never
/// cached) so <see cref="Strings"/> labels resolve under the current UI culture.
/// </summary>
public static class FooterContent
{
    /// <summary>Returns the footer sections in display order.</summary>
    public static IReadOnlyList<FooterSection> Sections() =>
    [
        new(Strings.Footer_Shop_Title,
        [
            new(Strings.Footer_Shop_Disposables),
            new(Strings.Footer_Shop_Pods),
            new(Strings.Footer_Shop_ELiquids),
            new(Strings.Footer_Shop_Accessories),
            new(Strings.Footer_Shop_Coil),
        ]),
        new(Strings.Footer_Help_Title,
        [
            new(Strings.Footer_Help_CustomerService),
            new(Strings.Footer_Help_TrackOrders),
            new(Strings.Footer_Help_ReturnRefunds),
        ]),
        new(Strings.Footer_Legal_Title,
        [
            new(Strings.Footer_Legal_Terms),
            new(Strings.Footer_Legal_Privacy),
            new(Strings.Footer_Legal_Return),
            new(Strings.Footer_Legal_Age),
        ]),
        new(Strings.Footer_Contact_Title,
        [
            new(Strings.Footer_Contact_Email, MailTo(Strings.Footer_Contact_Email)),
            new(Strings.Footer_Contact_Phone, Tel(Strings.Footer_Contact_Phone)),
            new(Strings.Footer_Contact_Address),
        ]),
    ];

    private static string MailTo(string email) => $"mailto:{email}";

    private static string Tel(string phone)
    {
        var dialable = new string([.. phone.Where(c => char.IsDigit(c) || c == '+')]);
        return $"tel:{dialable}";
    }
}
