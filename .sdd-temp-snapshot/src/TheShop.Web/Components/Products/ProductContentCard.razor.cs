using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Utilities;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Products;

/// <summary>
/// The product form's inline Content card (Figma node <c>2816:6685</c>): a rich-text description
/// section above a specification-rows section, mapping 1:1 to the design's "CONTENT" card
/// (plan Decision 13). The description section owns a <see cref="ShopRichTextEditor"/>, which
/// mounts Quill's own <c>snow</c> toolbar directly (Decision 2 waiver) — this card no longer
/// drives formatting itself. Specification rows keep the order they are added in; nothing
/// rearranges them (Decision 10) — removing a row renumbers the rest by list order and moves
/// focus to the next row's name field, or to "Add specification" when the removed row was last
/// (Decision 11).
/// </summary>
public partial class ProductContentCard : MudComponentBase
{
    /// <summary>The description's current HTML. Use with <c>@bind-Description</c>.</summary>
    [Parameter, EditorRequired]
    public string? Description { get; set; }

    /// <summary>Fires with the description's current HTML whenever the staff member edits it.</summary>
    [Parameter, EditorRequired]
    public EventCallback<string?> DescriptionChanged { get; set; }

    /// <summary>The saved specification rows, loaded once (Edit mode); empty for a new product.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<ProductSpecificationDto> InitialSpecifications { get; set; } = [];

    /// <summary>Fires with the card's current specification rows whenever they change.</summary>
    [Parameter, EditorRequired]
    public EventCallback<ProductSpecificationsState> StateChanged { get; set; }

    /// <summary>
    /// Row positions a failed save named (<c>Result.ErrorArgs</c>), so the omission shows up on
    /// the row the staff member has to fix rather than only in the message (RULE-3, RULE-4).
    /// </summary>
    [Parameter] public IReadOnlyList<int> FlaggedRowPositions { get; set; } = [];

    [Parameter] public bool Disabled { get; set; }

    private sealed class SpecificationRow
    {
        public required Guid Id { get; init; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private const string VisuallyHiddenStyle =
        "position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0;";

    private readonly List<SpecificationRow> _specifications = [];
    private readonly Dictionary<Guid, MudTextField<string>> _nameFieldRefs = [];
    private ShopRichTextEditor _richTextEditor = default!;
    private MudButton _addButtonRef = default!;
    private int _textLength;
    private bool _showPasteNotice;
    private string _liveAnnouncement = string.Empty;
    private bool _initialized;
    private Guid? _pendingFocusRowId;
    private bool _pendingFocusAddButton;
    private readonly string _descriptionLabelId = $"product-content-description-label-{Guid.NewGuid():N}";

    protected string Classname => new CssBuilder("product-content-card").AddClass(Class).Build();
    protected string Stylename => new StyleBuilder().AddStyle(Style).Build();

    /// <inheritdoc/>
    protected override Task OnParametersSetAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;

        foreach (var row in InitialSpecifications.OrderBy(s => s.Position))
            _specifications.Add(new SpecificationRow { Id = row.Id, Name = row.Name, Value = row.Value });

        // Seeds the form's save payload with the loaded rows even when the staff member never
        // touches this card, so an edit save that changes nothing else does not wipe them out.
        return NotifyAsync();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingFocusRowId is Guid rowId)
        {
            _pendingFocusRowId = null;
            if (_nameFieldRefs.TryGetValue(rowId, out var field))
                await field.FocusAsync();
        }
        else if (_pendingFocusAddButton)
        {
            _pendingFocusAddButton = false;
            await _addButtonRef.FocusAsync();
        }
    }

    private Task OnDescriptionChangedAsync(string? html) => DescriptionChanged.InvokeAsync(html);

    private void OnTextLengthChanged(int length) => _textLength = length;

    private Task OnUnsupportedPasteAsync()
    {
        _showPasteNotice = true;
        return Task.CompletedTask;
    }

    private Task AddRowAsync()
    {
        _specifications.Add(new SpecificationRow { Id = Guid.NewGuid() });
        return NotifyAsync();
    }

    private Task OnRowNameChangedAsync(SpecificationRow row, string value)
    {
        row.Name = value;
        return NotifyAsync();
    }

    private Task OnRowValueChangedAsync(SpecificationRow row, string value)
    {
        row.Value = value;
        return NotifyAsync();
    }

    private Task RemoveRowAsync(SpecificationRow row)
    {
        var index = _specifications.IndexOf(row);
        _specifications.Remove(row);
        _nameFieldRefs.Remove(row.Id);

        _liveAnnouncement = Strings.ProductSpecifications_RowRemovedAnnouncement;

        if (index < _specifications.Count)
            _pendingFocusRowId = _specifications[index].Id;
        else
            _pendingFocusAddButton = true;

        return NotifyAsync();
    }

    private bool IsFlagged(int position) =>
        FlaggedRowPositions.Contains(position) || DuplicateNamePositions().Contains(position);

    private HashSet<int> DuplicateNamePositions() =>
        [.. _specifications
            .Select((row, position) => (row, position))
            .Where(x => !string.IsNullOrWhiteSpace(x.row.Name))
            .GroupBy(x => x.row.Name.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Select(x => x.position))];

    private Task NotifyAsync()
    {
        var inputs = _specifications
            .Select((row, position) => new SpecificationInput(row.Id, row.Name, row.Value, position))
            .ToList();

        return StateChanged.InvokeAsync(new ProductSpecificationsState(inputs));
    }
}
