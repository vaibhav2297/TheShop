using Microsoft.Extensions.Primitives;

namespace TheShop.Web.Common;

/// <summary>
/// Contract for a page's state that round-trips through the URL query string, giving the page
/// deep-linkable, shareable URLs and browser Back/Forward support. Implementers are immutable
/// records that project their fields to query parameters and reconstruct themselves from a parsed
/// query. Consumed by <see cref="QueryStatePageBase{TState}"/>.
/// </summary>
/// <typeparam name="TSelf">The implementing type itself.</typeparam>
public interface IUrlQueryState<TSelf> where TSelf : IUrlQueryState<TSelf>
{
    /// <summary>
    /// Projects this state to query parameters. Each value may be a <see cref="string"/>,
    /// a <c>string[]</c> (rendered as repeated <c>key=a&amp;key=b</c> params), or any primitive
    /// that <c>NavigationManager.GetUriWithQueryParameters</c> supports. Return only the
    /// non-default values — omitted keys keep the URL clean.
    /// </summary>
    IReadOnlyDictionary<string, object?> ToQueryParameters();

    /// <summary>
    /// Reconstructs the state from a parsed query string. Unknown or malformed parameters must
    /// fall back to defaults instead of throwing, so a hand-edited URL can never break the page.
    /// </summary>
    static abstract TSelf FromQuery(IReadOnlyDictionary<string, StringValues> query);
}
