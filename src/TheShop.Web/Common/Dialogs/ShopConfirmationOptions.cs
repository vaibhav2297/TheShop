using TheShop.Web.Resources;

namespace TheShop.Web.Common.Dialogs;

/// <summary>Already-localized confirmation copy and the action's destructive intent.</summary>
public sealed record ShopConfirmationOptions(string Title, string Body, string ConfirmLabel, bool Destructive = false)
{
    /// <summary>Visible label for the safe, initially focused action.</summary>
    public string CancelLabel { get; init; } = Strings.Cancel;
}
