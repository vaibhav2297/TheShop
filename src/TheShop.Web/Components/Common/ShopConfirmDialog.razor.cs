using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Reusable confirm/cancel dialog, shown via <see cref="IDialogService.ShowAsync{TComponent}(DialogParameters)"/>.
/// Backs every deactivate and delete confirmation (RULE-7, RULE-14) so the four sites share one
/// mechanics implementation instead of four inline <see cref="MudDialog"/> blocks. All text is
/// supplied by the caller as already-localized strings (Rule 11 Pattern 1 at the call site) —
/// the dialog itself resolves no resource keys.
/// </summary>
public partial class ShopConfirmDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The dialog's title.</summary>
    [Parameter, EditorRequired]
    public string TitleText { get; set; } = string.Empty;

    /// <summary>The dialog's body — states the action's consequence (RULE-7: permanence for delete).</summary>
    [Parameter, EditorRequired]
    public string BodyText { get; set; } = string.Empty;

    /// <summary>The confirm button's label.</summary>
    [Parameter, EditorRequired]
    public string ConfirmLabel { get; set; } = string.Empty;

    /// <summary>The cancel button's label.</summary>
    [Parameter]
    public string CancelLabel { get; set; } = Resources.Strings.Cancel;

    /// <summary>The confirm button's color — <see cref="Color.Error"/> for a destructive delete.</summary>
    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Primary;

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));

    private void Cancel() => MudDialog.Cancel();
}
