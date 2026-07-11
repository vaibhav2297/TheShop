using static Supabase.Postgrest.Constants;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using TheShop.Application.Common.Models;

namespace TheShop.Infrastructure.Persistence.Paging;

/// <summary>
/// Reusable paging helper for Postgrest queries. Centralizes the count + range + fetch
/// boilerplate so any repository can page a Supabase table without repeating the
/// (off-by-one-prone) range arithmetic or the count/page query duplication.
/// </summary>
internal static class PostgrestPaginationExtensions
{
    /// <summary>
    /// Executes a paged read for <typeparamref name="TRecord"/>: counts the total matches and
    /// fetches the requested page <em>concurrently</em> (they are independent requests, so there
    /// is no reason to wait for one before issuing the other). Filters are supplied by
    /// <paramref name="filteredQueryFactory"/>, which must return a <em>fresh</em> table with the
    /// filters already applied — it is invoked twice (once for the count, once for the page) so
    /// both queries stay in sync. Ordering is applied to the page query only, via
    /// <paramref name="applyOrdering"/>.
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
        var countTask = filteredQueryFactory().Count(countType, ct);

        var pageQuery = filteredQueryFactory();
        applyOrdering?.Invoke(pageQuery);

        var (from, to) = pagination.ToInclusiveRange();
        pageQuery.Range(from, to);
        var pageTask = pageQuery.Get(ct);

        await Task.WhenAll(countTask, pageTask);

        var totalCount = await countTask;
        var response = await pageTask;

        return new PagedResult<TRecord>(response.Models, pagination.Page, pagination.PageSize, totalCount);
    }
}
