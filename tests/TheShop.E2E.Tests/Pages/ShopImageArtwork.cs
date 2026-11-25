using System.Globalization;
using Microsoft.Playwright;

namespace TheShop.E2E.Tests.Pages;

/// <summary>
/// Deterministic image sources for .specs/shop-image journeys. Every source is an SVG with an exact
/// intrinsic size, so tall, wide, and exact-ratio artwork never depends on a third-party image host.
/// Requests for <see cref="Url"/> are answered by the browser context itself; seeded placeholder URLs
/// (placehold.co) are answered the same way through <see cref="FulfillAsync"/>.
/// </summary>
public static class ShopImageArtwork
{
    /// <summary>Glob for every seeded third-party placeholder image URL.</summary>
    public const string SeededPlaceholderGlob = "https://placehold.co/**";

    private const string PathPrefix = "/e2e-shop-image/";

    /// <summary>A same-origin URL whose response is an SVG of exactly <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static string Url(int width, int height, string name = "art") => $"{PathPrefix}{name}-{width}x{height}.svg";

    /// <summary>A same-origin URL that always fails to load.</summary>
    public static string BrokenUrl(string name = "broken") => $"{PathPrefix}{name}-missing.svg";

    /// <summary>Answers <see cref="Url"/> requests with sized SVGs and <see cref="BrokenUrl"/> requests with a network failure.</summary>
    public static Task RouteAsync(IBrowserContext context) =>
        context.RouteAsync($"**{PathPrefix}**", route =>
        {
            var file = route.Request.Url[(route.Request.Url.LastIndexOf('/') + 1)..];
            var size = file[(file.LastIndexOf('-') + 1)..^".svg".Length].Split('x');
            return size.Length == 2
                && int.TryParse(size[0], CultureInfo.InvariantCulture, out var width)
                && int.TryParse(size[1], CultureInfo.InvariantCulture, out var height)
                ? FulfillAsync(route, width, height)
                : route.AbortAsync();
        });

    /// <summary>
    /// The record label a seeded placeholder URL was generated for (its decoded <c>text</c> query
    /// value, e.g. a product, brand, or category name), or an empty string.
    /// </summary>
    public static string SeededLabel(IRequest request) =>
        System.Web.HttpUtility.ParseQueryString(new Uri(request.Url).Query)["text"] ?? "";

    /// <summary>Answers <paramref name="route"/> with an SVG of exactly <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static Task FulfillAsync(IRoute route, int width, int height) =>
        route.FulfillAsync(new()
        {
            ContentType = "image/svg+xml",
            Body = $"""
                <svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
                  <rect width="{width}" height="{height}" fill="#c0392b"/>
                  <rect x="{width / 4}" y="{height / 4}" width="{width / 2}" height="{height / 2}" fill="#27ae60"/>
                </svg>
                """,
        });
}
