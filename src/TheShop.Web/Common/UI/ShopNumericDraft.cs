namespace TheShop.Web.Common.UI;

/// <summary>Retains a numeric editor's uncommitted text when a virtualized row leaves and re-enters the viewport. Use one instance per field.</summary>
public sealed class ShopNumericDraft
{
    internal string? Text { get; set; }
    internal decimal? Supplied { get; set; }
    internal decimal? Committed { get; set; }
    internal bool Initialized { get; set; }
    internal bool HasDraft { get; set; }
    internal string? Error { get; set; }

    internal void Reset(decimal? value)
    {
        Initialized = false;
        Supplied = value;
        Committed = value;
        Text = value?.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
        HasDraft = false;
        Error = null;
    }
}
