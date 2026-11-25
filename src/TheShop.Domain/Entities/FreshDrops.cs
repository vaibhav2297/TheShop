using TheShop.Domain.Enums;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>The single staff-managed Fresh Drops configuration.</summary>
public sealed class FreshDrops
{
    private readonly List<Guid> _manualProductIds = [];

    /// <summary>The singleton identity.</summary>
    public short Id => 1;
    /// <summary>The section title.</summary>
    public string Title { get; private set; } = string.Empty;
    /// <summary>Optional button text.</summary>
    public string? ButtonLabel { get; private set; }
    /// <summary>Optional button destination.</summary>
    public string? ButtonLink { get; private set; }
    /// <summary>The active product selection mode.</summary>
    public FreshDropsSelectionMode SelectionMode { get; private set; }
    /// <summary>Saved manual selection order.</summary>
    public IReadOnlyList<Guid> ManualProductIds => _manualProductIds;
    /// <summary>Whether the section is visible.</summary>
    public bool IsVisible { get; private set; }

    private FreshDrops() { }

    /// <summary>Creates a validated configuration. Newest mode clears manual IDs.</summary>
    public static FreshDrops Create(string title, string? buttonLabel, string? buttonLink,
        FreshDropsSelectionMode selectionMode, IEnumerable<Guid> manualProductIds, bool isVisible)
    {
        var drops = new FreshDrops();
        drops.ChangeContent(title, buttonLabel, buttonLink);
        drops.ChangeSelection(selectionMode, manualProductIds);
        drops.SetVisibility(isVisible);
        return drops;
    }

    /// <summary>Changes the title and optional button pair together.</summary>
    public void ChangeContent(string title, string? buttonLabel, string? buttonLink)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("FreshDrops_TitleRequired");
        if (string.IsNullOrWhiteSpace(buttonLabel) != string.IsNullOrWhiteSpace(buttonLink))
            throw new DomainException("FreshDrops_ButtonPairRequired");
        if (!string.IsNullOrWhiteSpace(buttonLink) && !IsSafeDestination(buttonLink))
            throw new DomainException("FreshDrops_ButtonLinkInvalid");
        Title = title.Trim();
        ButtonLabel = string.IsNullOrWhiteSpace(buttonLabel) ? null : buttonLabel.Trim();
        ButtonLink = string.IsNullOrWhiteSpace(buttonLink) ? null : buttonLink.Trim();
    }

    /// <summary>Changes mode and ordered IDs; saved Newest mode has no manual IDs.</summary>
    public void ChangeSelection(FreshDropsSelectionMode mode, IEnumerable<Guid> manualProductIds)
    {
        if (!Enum.IsDefined(mode)) throw new DomainException("FreshDrops_SelectionInvalid");
        var ids = mode == FreshDropsSelectionMode.Newest ? new List<Guid>() : manualProductIds.ToList();
        if (ids.Count > 3 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new DomainException("FreshDrops_SelectionInvalid");
        _manualProductIds.Clear();
        _manualProductIds.AddRange(ids);
        SelectionMode = mode;
    }

    /// <summary>Changes visibility.</summary>
    public void SetVisibility(bool isVisible) => IsVisible = isVisible;

    private static bool IsSafeDestination(string value)
    {
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')) return false;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '%') continue;
            if (i + 2 >= value.Length || !Uri.IsHexDigit(value[i + 1]) || !Uri.IsHexDigit(value[i + 2])) return false;
            i += 2;
        }
        if (value.StartsWith('/')) return !value.StartsWith("//", StringComparison.Ordinal)
            && Uri.TryCreate(value, UriKind.Relative, out _);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }
}
