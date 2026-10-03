using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.Forms;

namespace TheShop.Web.Components.Common;

/// <summary>A text input that updates its EditContext on each keystroke, preserving the migrated immediate-validation behavior.</summary>
public partial class ShopTextInput : InputBase<string?>
{
    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, out string? result,
        [NotNullWhen(false)] out string? validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null;
        return true;
    }
}
