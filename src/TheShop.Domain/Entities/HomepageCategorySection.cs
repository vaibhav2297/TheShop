using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>The single staff-managed homepage category configuration.</summary>
public sealed class HomepageCategorySection
{
    private readonly List<Guid> _categoryIds = [];

    /// <summary>The singleton identity.</summary>
    public short Id => 1;
    /// <summary>The section title.</summary>
    public string Title { get; private set; } = string.Empty;
    /// <summary>The button text.</summary>
    public string ButtonLabel { get; private set; } = string.Empty;
    /// <summary>The button destination.</summary>
    public string ButtonLink { get; private set; } = string.Empty;
    /// <summary>Three category IDs in display order.</summary>
    public IReadOnlyList<Guid> CategoryIds => _categoryIds;
    /// <summary>Whether the section is visible.</summary>
    public bool IsVisible { get; private set; }

    private HomepageCategorySection() { }

    /// <summary>Creates a complete configuration.</summary>
    public static HomepageCategorySection Create(string title, string buttonLabel, string buttonLink,
        IEnumerable<Guid> categoryIds, bool isVisible)
    {
        var section = new HomepageCategorySection();
        section.Update(title, buttonLabel, buttonLink, categoryIds, isVisible);
        return section;
    }

    /// <summary>Replaces all editable fields after validating the complete configuration.</summary>
    public void Update(string title, string buttonLabel, string buttonLink,
        IEnumerable<Guid> categoryIds, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("HomepageCategory_TitleRequired");
        if (string.IsNullOrWhiteSpace(buttonLabel)) throw new DomainException("HomepageCategory_ButtonLabelRequired");
        if (string.IsNullOrWhiteSpace(buttonLink)) throw new DomainException("HomepageCategory_ButtonLinkRequired");
        if (!IsSafeDestination(buttonLink.Trim())) throw new DomainException("HomepageCategory_ButtonLinkInvalid");

        var ids = categoryIds?.ToList() ?? [];
        if (ids.Count != 3 || ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != 3)
            throw new DomainException("HomepageCategory_SelectionInvalid");

        Title = title.Trim();
        ButtonLabel = buttonLabel.Trim();
        ButtonLink = buttonLink.Trim();
        _categoryIds.Clear();
        _categoryIds.AddRange(ids);
        IsVisible = isVisible;
    }

    /// <summary>Changes visibility without changing the saved selection.</summary>
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
