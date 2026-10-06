using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using TheShop.Application.Common.Models;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Common;

/// <summary>
/// Displays command validation failures on matching form properties, with unresolved errors in a form summary.
/// </summary>
public partial class ShopCommandValidation : ComponentBase, IDisposable
{
    [Inject] private IStringLocalizer<Strings> Localizer { get; set; } = default!;

    /// <summary>
    /// The containing form's editing context.
    /// </summary>
    [CascadingParameter] public EditContext? CurrentEditContext { get; set; }

    /// <summary>
    /// Optional exact command-path to form-path overrides. Unlisted paths map by matching property names.
    /// </summary>
    [Parameter] public IReadOnlyDictionary<string, string>? FieldMap { get; set; }

    private EditContext? _editContext;
    private ValidationMessageStore? _messages;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
            throw new InvalidOperationException($"{nameof(ShopCommandValidation)} requires an {nameof(EditForm)}.");

        if (ReferenceEquals(_editContext, CurrentEditContext))
            return;

        Detach();
        _editContext = CurrentEditContext;
        _messages = new ValidationMessageStore(_editContext);
        _editContext.OnFieldChanged += OnFieldChanged;
    }

    /// <summary>
    /// Replaces this component's errors, resolves their resource keys, and notifies the form.
    /// </summary>
    /// <param name="errors">Command paths supporting public properties, nested objects, and zero-based list or array indices.</param>
    public void ShowErrors(IReadOnlyList<FieldValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (_editContext is null || _messages is null)
            throw new InvalidOperationException("The validation component is not attached to a form.");

        _messages.Clear();
        foreach (var error in errors)
        {
            var path = FieldMap?.TryGetValue(error.PropertyName, out var mappedPath) == true
                ? mappedPath : error.PropertyName;
            var field = ResolveField(_editContext.Model, path)
                ?? new FieldIdentifier(_editContext.Model, string.Empty);
            _messages.Add(field, Localizer[error.MessageKey]);
        }

        _editContext.NotifyValidationStateChanged();
    }

    /// <summary>
    /// Clears this component's errors without removing messages owned by other validators.
    /// </summary>
    public void Clear()
    {
        _messages?.Clear();
        _editContext?.NotifyValidationStateChanged();
    }

    private void OnFieldChanged(object? sender, FieldChangedEventArgs args)
    {
        _messages?.Clear(args.FieldIdentifier);
        _editContext?.NotifyValidationStateChanged();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "Bound form properties are preserved by input ValueExpression bindings. Missing metadata falls back to the form summary.")]
    private static FieldIdentifier? ResolveField(object model, string path)
    {
        object? current = model;
        while (current is not null and not string && !current.GetType().IsValueType && !string.IsNullOrEmpty(path))
        {
            if (path[0] == '[')
            {
                var closing = path.IndexOf(']');
                if (closing < 0 || current is not IList list
                    || !int.TryParse(path.AsSpan(1, closing - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                    || index >= list.Count)
                    return null;

                if (list is Array array && (array.Rank != 1 || array.GetLowerBound(0) != 0))
                    return null;

                if (closing == path.Length - 1)
                    return new FieldIdentifier(current, index.ToString(CultureInfo.InvariantCulture));

                current = list[index];
                path = path[(closing + 1)..];
                if (path[0] == '.')
                    path = path[1..];
                else if (path[0] != '[')
                    return null;
                continue;
            }

            var separator = path.IndexOfAny(['.', '[']);
            var name = separator < 0 ? path : path[..separator];
            var property = current.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetMethod?.IsPublic != true || property.GetIndexParameters().Length != 0)
                return null;

            if (separator < 0)
                return new FieldIdentifier(current, name);

            current = property.GetValue(current);
            path = path[separator] == '.' ? path[(separator + 1)..] : path[separator..];
        }

        return null;
    }

    private void Detach()
    {
        if (_editContext is null)
            return;

        _editContext.OnFieldChanged -= OnFieldChanged;
        _messages?.Clear();
        _editContext.NotifyValidationStateChanged();
        _messages = null;
        _editContext = null;
    }

    /// <summary>
    /// Removes this component's messages and releases its field-change subscription.
    /// </summary>
    public void Dispose() => Detach();
}
