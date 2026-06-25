using MudBlazor;
using TheShop.Web.Resources;

namespace TheShop.Web.Common;

/// <summary>
/// Fluent builder for constructing a breadcrumb trail from the site or admin hierarchy.
/// Pages call one of the static entry points, chain <see cref="Add"/> calls for
/// intermediate levels, terminate with <see cref="Current"/>, and pass the result
/// to <see cref="State.BreadcrumbState.Set"/>.
/// </summary>
public sealed class BreadcrumbTrail
{
    private readonly List<BreadcrumbItem> _items;

    private BreadcrumbTrail(List<BreadcrumbItem> items) => _items = items;

    /// <summary>
    /// Starts a storefront trail, seeding it with the Home root item.
    /// </summary>
    public static BreadcrumbTrail Storefront() =>
        new([new BreadcrumbItem(Strings.Nav_Home, Routes.Home)]);

    /// <summary>
    /// Starts an admin trail, seeding it with the Dashboard root item.
    /// </summary>
    /// <remarks>
    /// Only call this from pages that are already protected by admin authorization and
    /// the corresponding Supabase RLS policies. See <see cref="Routes.Admin"/> for details.
    /// </remarks>
    public static BreadcrumbTrail Admin() =>
        new([new BreadcrumbItem(Strings.Nav_Dashboard, Routes.Admin.Dashboard)]);

    /// <summary>
    /// Appends a clickable intermediate crumb. Pass <c>null</c> for
    /// <paramref name="href"/> to render the level as plain text (e.g. a removed parent).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="href"/> contains a disallowed URI scheme (anything other
    /// than a relative path or <c>https://</c>). This prevents <c>javascript:</c>,
    /// <c>data:</c>, and similar injection vectors from reaching the rendered anchor.
    /// </exception>
    public BreadcrumbTrail Add(string text, string? href)
    {
        if (href is not null && !IsAllowedHref(href))
            throw new ArgumentException($"Disallowed scheme in breadcrumb href: '{href}'", nameof(href));
        _items.Add(new BreadcrumbItem(text, href, disabled: href is null));
        return this;
    }

    // Relative paths and https are the only safe href values for rendered <a> tags.
    private static bool IsAllowedHref(string href) =>
        href.StartsWith('/') ||
        href.StartsWith("~/") ||
        href.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Appends the non-clickable final crumb representing the current page,
    /// then returns the completed trail.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Current(string text)
    {
        _items.Add(new BreadcrumbItem(text, href: null, disabled: true));
        return _items.AsReadOnly();
    }

    /// <summary>
    /// Returns the trail built so far without appending a current-page crumb.
    /// Prefer <see cref="Current"/> for a complete trail.
    /// </summary>
    public IReadOnlyList<BreadcrumbItem> Build() => _items.AsReadOnly();
}
