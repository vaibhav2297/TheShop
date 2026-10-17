using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;

namespace TheShop.Web.Components.Products;

/// <summary>Ordered specification editor with add, remove, server errors and focus recovery.</summary>
public partial class ProductContentCard : ShopComponentBase
{
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

    private readonly List<SpecificationRow> _specifications = [];
    private readonly Dictionary<Guid, ShopTextField> _nameFieldRefs = [];
    private ShopButton _addButtonRef = default!;
    private string _liveAnnouncement = string.Empty;
    private bool _initialized;
    private Guid? _pendingFocusRowId;
    private bool _pendingFocusAddButton;
    protected string Classname => ShopCssClass.Join("shop-native", "shop-product-specifications", Class);

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

    private Task AddRowAsync()
    {
        _specifications.Add(new SpecificationRow { Id = Guid.NewGuid() });
        return NotifyAsync();
    }

    private Task OnRowNameChangedAsync(SpecificationRow row, string? value)
    {
        row.Name = value ?? string.Empty;
        return NotifyAsync();
    }

    private Task OnRowValueChangedAsync(SpecificationRow row, string? value)
    {
        row.Value = value ?? string.Empty;
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

    private bool IsFlagged(int position) => FlaggedRowPositions.Contains(position);

    private string? RowErrorId(SpecificationRow row, int position) =>
        IsFlagged(position) ? $"product-specification-error-{row.Id:N}" : null;

    private Task NotifyAsync()
    {
        var inputs = _specifications
            .Select((row, position) => new SpecificationInput(row.Id, row.Name, row.Value, position))
            .ToList();

        return StateChanged.InvokeAsync(new ProductSpecificationsState(inputs));
    }
}
