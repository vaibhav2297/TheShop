using System.Globalization;
using static Supabase.Postgrest.Constants;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using TheShop.Application.Common.Models;

namespace TheShop.Infrastructure.Persistence.Paging;

/// <summary>
/// Reusable paging helper for Postgrest queries. Centralizes the range + count boilerplate so any
/// repository can page a Supabase table without repeating the (off-by-one-prone) range arithmetic
/// or the count/page query duplication.
/// </summary>
internal static class PostgrestPaginationExtensions
{
    private const string ContentRangeHeader = "Content-Range";

    /// <summary>
    /// Executes a paged read for <typeparamref name="TRecord"/> in a <em>single</em> request: the
    /// page is fetched with <c>Prefer: count=exact</c> so PostgREST returns the total match count in
    /// the response's <c>Content-Range</c> header alongside the rows — no separate count round-trip.
    /// Filters are supplied by <paramref name="filteredQueryFactory"/>, which must return a
    /// <em>fresh</em> table with the filters already applied (it is called again only for the rare
    /// fallback count). Ordering is applied to the page query via <paramref name="applyOrdering"/>.
    /// </summary>
    /// <param name="filteredQueryFactory">Produces a fresh, filtered query builder on each call.</param>
    /// <param name="applyOrdering">Applies sort to the page query; <c>null</c> for no explicit order.</param>
    /// <param name="pagination">The requested page position and size.</param>
    /// <param name="ct">A token to cancel the underlying requests.</param>
    /// <param name="countType">How the total count is computed; defaults to an exact count.</param>
    /// <returns>The page of records plus the total match count.</returns>
    public static async Task<PagedResult<TRecord>> GetPagedAsync<TRecord>(
        this Func<IPostgrestTable<TRecord>> filteredQueryFactory,
        Action<IPostgrestTable<TRecord>>? applyOrdering,
        PaginationRequest pagination,
        CancellationToken ct,
        CountType countType = CountType.Exact)
        where TRecord : BaseModel, new()
    {
        var pageQuery = filteredQueryFactory();
        applyOrdering?.Invoke(pageQuery);

        var (from, to) = pagination.ToInclusiveRange();
        pageQuery.Range(from, to);

        // Single-request path: ask PostgREST to fold the total count into this request's
        // Content-Range header instead of paying for a second (HEAD) count round-trip. Only the
        // concrete Table<T> exposes the settable header factory needed to add the Prefer header.
        if (pageQuery is Table<TRecord> table)
        {
            RequestExactCount(table, countType);
            var response = await table.Get(ct);

            if (TryParseContentRangeTotal(response.ResponseMessage) is int total)
                return new PagedResult<TRecord>(response.Models, pagination.Page, pagination.PageSize, total);

            // A proxy stripped the count (or the server didn't honour the preference): fall back to
            // a dedicated count so pagination stays correct. Rare, and still just one extra request.
            var fallbackTotal = await filteredQueryFactory().Count(countType, ct);
            return new PagedResult<TRecord>(response.Models, pagination.Page, pagination.PageSize, fallbackTotal);
        }

        // Defensive path for a non-Table implementation (e.g. a test double): count and page as two
        // independent, concurrent requests.
        var countTask = filteredQueryFactory().Count(countType, ct);
        var pageTask = pageQuery.Get(ct);
        await Task.WhenAll(countTask, pageTask);
        return new PagedResult<TRecord>((await pageTask).Models, pagination.Page, pagination.PageSize, await countTask);
    }

    // Wraps (never replaces) the table's header factory so the client's auth headers survive, adding
    // the Prefer: count=<type> preference that makes a ranged GET report the total in Content-Range.
    private static void RequestExactCount<TRecord>(Table<TRecord> table, CountType countType)
        where TRecord : BaseModel, new()
    {
        var mapping = countType switch
        {
            CountType.Planned => "planned",
            CountType.Estimated => "estimated",
            _ => "exact",
        };

        var inner = table.GetHeaders;
        table.GetHeaders = () =>
        {
            var headers = inner?.Invoke() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers["Prefer"] = headers.TryGetValue("Prefer", out var existing) && !string.IsNullOrEmpty(existing)
                ? $"{existing},count={mapping}"
                : $"count={mapping}";
            return headers;
        };
    }

    // Content-Range looks like "0-11/234", "*/234", or "0-11/*"; the segment after the final slash
    // is the total (or "*" when the count wasn't computed, which yields null and triggers fallback).
    private static int? TryParseContentRangeTotal(HttpResponseMessage? message)
    {
        if (message is null || !TryGetHeaderValue(message, ContentRangeHeader, out var range))
            return null;

        var slash = range.LastIndexOf('/');
        if (slash < 0 || slash == range.Length - 1)
            return null;

        return int.TryParse(range[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var total)
            ? total
            : null;
    }

    // Content-Range is a content header, but check the message headers too so we find it either way.
    private static bool TryGetHeaderValue(HttpResponseMessage message, string name, out string value)
    {
        if (message.Headers.TryGetValues(name, out var values)
            || (message.Content?.Headers.TryGetValues(name, out values) ?? false))
        {
            value = values.FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrEmpty(value);
        }

        value = string.Empty;
        return false;
    }
}
