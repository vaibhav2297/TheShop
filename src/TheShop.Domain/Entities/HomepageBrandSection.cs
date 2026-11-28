using TheShop.Domain.Exceptions;

namespace TheShop.Domain.Entities;

/// <summary>The single staff-managed homepage brand configuration.</summary>
public sealed class HomepageBrandSection
{
    private readonly List<Guid> _brandIds = [];

    /// <summary>The singleton identity.</summary>
    public short Id => 1;
    /// <summary>The section title.</summary>
    public string Title { get; private set; } = string.Empty;
    /// <summary>The button text.</summary>
    public string ButtonLabel { get; private set; } = string.Empty;
    /// <summary>The button destination.</summary>
    public string ButtonLink { get; private set; } = string.Empty;
    /// <summary>Up to six brand IDs in display order.</summary>
    public IReadOnlyList<Guid> BrandIds => _brandIds;
    /// <summary>Whether the section is visible.</summary>
    public bool IsVisible { get; private set; }

    private HomepageBrandSection() { }

    /// <summary>Creates a complete configuration.</summary>
    public static HomepageBrandSection Create(string title, string buttonLabel, string buttonLink,
        IEnumerable<Guid> brandIds, bool isVisible)
    {
        var section = new HomepageBrandSection();
        section.Update(title, buttonLabel, buttonLink, brandIds, isVisible);
        return section;
    }

    /// <summary>Replaces all editable fields after validating the complete configuration.</summary>
    public void Update(string title, string buttonLabel, string buttonLink,
        IEnumerable<Guid> brandIds, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("HomepageBrand_TitleRequired");
        if (string.IsNullOrWhiteSpace(buttonLabel)) throw new DomainException("HomepageBrand_ButtonLabelRequired");
        if (string.IsNullOrWhiteSpace(buttonLink)) throw new DomainException("HomepageBrand_ButtonLinkRequired");
        if (!IsSafeDestination(buttonLink.Trim())) throw new DomainException("HomepageBrand_ButtonLinkInvalid");

        var ids = brandIds?.ToList() ?? [];
        if (ids.Count > 6 || (isVisible && ids.Count == 0) ||
            ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new DomainException("HomepageBrand_SelectionInvalid");

        Title = title.Trim();
        ButtonLabel = buttonLabel.Trim();
        ButtonLink = buttonLink.Trim();
        _brandIds.Clear();
        _brandIds.AddRange(ids);
        IsVisible = isVisible;
    }

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
