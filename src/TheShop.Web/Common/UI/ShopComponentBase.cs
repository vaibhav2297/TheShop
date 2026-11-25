using Microsoft.AspNetCore.Components;

namespace TheShop.Web.Common.UI;

/// <summary>Shared root styling and attribute contract for native visual components.</summary>
public abstract class ShopComponentBase : ComponentBase
{
    /// <summary>Additional CSS classes applied to the component's documented root.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Optional dynamic styles applied to the component's documented root.</summary>
    [Parameter] public string? Style { get; set; }

    /// <summary>Root HTML attributes; explicit component attributes take precedence.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
