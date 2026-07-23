using Supabase.Postgrest;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Brands;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="IBrandRepository"/>.
/// </summary>
public sealed class SupabaseBrandRepository(Supabase.Client client) : IBrandRepository
{
    // Matches the DB unique index name from migration 0012 (ux_brands_normalized_name),
    // the race-condition backstop behind the ExistsByNormalizedNameAsync pre-check.
    private const string NormalizedNameUniqueConstraint = "ux_brands_normalized_name";

    // Matches Postgres's auto-generated constraint name for `slug TEXT NOT NULL UNIQUE`
    // (migration 0002). Distinct names can still collapse to the same slug (e.g. "Test Verify"
    // vs "Test.Verify" both slugify to "test-verify"), so this is not just a race-condition
    // backstop like the one above — it is the only guard against that case.
    private const string SlugUniqueConstraint = "brands_slug_key";

    /// <inheritdoc/>
    public async Task<bool> ExistsByNormalizedNameAsync(string name, CancellationToken ct)
    {
        var trimmed = name.Trim();

        var response = await client
            .From<BrandRecord>()
            .Filter("name", Constants.Operator.ILike, trimmed)
            .Get(ct);

        return response.Models.Count > 0;
    }

    /// <inheritdoc/>
    public async Task<Result> AddAsync(Brand brand, CancellationToken ct)
    {
        var record = brand.ToRecord();

        try
        {
            await client.From<BrandRecord>().Insert(record, cancellationToken: ct);
            return Result.Ok();
        }
        catch (Exception ex) when (IsNormalizedNameUniqueViolation(ex))
        {
            return Result.Fail(BrandErrorKeys.AlreadyExists);
        }
        catch (Exception ex) when (IsSlugUniqueViolation(ex))
        {
            return Result.Fail(BrandErrorKeys.SlugConflict);
        }
    }

    private static bool IsNormalizedNameUniqueViolation(Exception ex) =>
        ex.Message.Contains(NormalizedNameUniqueConstraint, StringComparison.OrdinalIgnoreCase);

    private static bool IsSlugUniqueViolation(Exception ex) =>
        ex.Message.Contains(SlugUniqueConstraint, StringComparison.OrdinalIgnoreCase);
}
