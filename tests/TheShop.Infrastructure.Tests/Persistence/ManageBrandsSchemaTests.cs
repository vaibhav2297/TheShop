using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the manage-brands schema deltas (.specs/manage-brands/plan.md §10):
/// the dropped <c>brands.slug</c> column and its unique index (spec FR-9, Decision 6, AC-28), the
/// <c>brand_product_counts(uuid[])</c> and <c>delete_brands(uuid[])</c> <c>SECURITY DEFINER</c>
/// RPCs that answer RULE-6/RULE-13 questions no client-visible RLS policy can (Decisions 2/3), and
/// the <c>brands_admin_update</c>/<c>brands_admin_delete</c> policies (RULE-8). Exercised in
/// production by <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseBrandRepository"/>.
///
/// Spins up a real Postgres container (Testcontainers), reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (the same mechanism <see cref="SupabaseBrandRepositorySchemaTests"/>
/// uses for add-brand), then layers a minimal <c>brands</c>/<c>products</c> pair on top — the
/// slug-free, post-migration-0014 shape of <c>brands</c> plus just enough of <c>products</c>
/// (<c>brand_id</c>, <c>is_published</c>) for the two RPCs to answer against. RULE-2's uniqueness
/// backstop and the <c>brands_read</c>/<c>brands_admin_insert</c> policies are already covered by
/// <see cref="SupabaseBrandRepositorySchemaTests"/> for add-brand; this class covers only what
/// manage-brands adds or changes.
/// <see href=".specs/manage-brands/spec.md"/>
/// <see href=".specs/manage-brands/plan.md"/>
/// </summary>
public sealed class ManageBrandsSchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("shop_test")
        .WithUsername("shop")
        .WithPassword("shop")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(conn);
        await RbacTestSchema.ApplyRbacCoreAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await ApplyManageBrandsSchemaAsync(conn);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    // =========================================================================
    // Dropped slug column — a rename can no longer be refused for a derived-identifier clash
    // (spec FR-9, Decision 6, AC-28)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Schema_BrandsTable_HasNoSlugColumn()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'brands' AND column_name = 'slug'");

        Convert.ToInt32(count).Should().Be(0, "the brand's name-derived identifier is retired (FR-9)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task Schema_IdxBrandsNameLower_Exists()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn,
            "SELECT COUNT(*) FROM pg_indexes WHERE indexname = 'idx_brands_name_lower'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task UpdateBrand_WhenRenamedToADifferentPunctuationVariantOfAnotherBrandsName_Succeeds()
    {
        // AC-28: "Test Verify" and "Test.Verify" are distinct under RULE-2 — with the slug (and its
        // separate normalization) gone, only ux_brands_normalized_name governs uniqueness, and it
        // does not strip punctuation.
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, "Test Verify");
        var otherId = await InsertBrandAsync(conn, "Other Brand");

        var act = () => UpdateBrandNameAsync(conn, otherId, "Test.Verify");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task UpdateBrand_WhenRenamedToAnotherBrandsNameDifferingOnlyByCapitalization_ThrowsUniqueViolation()
    {
        // RULE-2 via the UPDATE path — the edit form's persistence route, not add-brand's INSERT.
        await using var conn = await OpenAsync();
        await InsertBrandAsync(conn, "Elf Bar");
        var otherId = await InsertBrandAsync(conn, "Lost Mary");

        var act = () => UpdateBrandNameAsync(conn, otherId, "ELF BAR");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task UpdateBrand_SavedWithItsOwnUnchangedName_Succeeds()
    {
        // AC-9: a no-op save must not collide with itself. The application layer achieves this via
        // ExistsByNormalizedNameAsync(excludeBrandId); at the storage level, updating a row to the
        // name it already holds is simply not a duplicate of any *other* row.
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");

        var act = () => UpdateBrandNameAsync(conn, id, "Elf Bar");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // brands_admin_update / brands_admin_delete RLS (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task UpdateBrand_WhenCallerHoldsBrandsEditPermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, "brands.edit");

        var act = () => UpdateBrandNameAsync(conn, id, "Lost Mary");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task UpdateBrand_WhenCallerLacksBrandsEditPermission_TheUpdateAffectsNoRows()
    {
        // Unlike an INSERT's WITH CHECK, a USING-only denial on UPDATE never raises — the
        // brands_admin_update policy just hides the row from the WHERE clause, so the statement
        // silently matches nothing rather than throwing 42501 (RULE-8).
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var affected = await UpdateBrandNameAsync(conn, id, "Lost Mary");

        affected.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrandRow_WhenCallerHoldsBrandsDeletePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, "brands.delete");

        var act = () => DeleteBrandRowAsync(conn, id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrandRow_WhenCallerLacksBrandsDeletePermission_TheDeleteAffectsNoRows()
    {
        // Same USING-only semantics as the UPDATE case above: brands_admin_delete hides the row
        // rather than raising, so the DELETE silently matches nothing (RULE-8).
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var affected = await DeleteBrandRowAsync(conn, id);

        affected.Should().Be(0);
    }

    // =========================================================================
    // products.brand_id FK — the last-resort guard behind RULE-6, even for a caller who holds
    // brands.delete (the RPC below is what stands between staff and this raw violation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrandRow_WhenReferencedByAProduct_ThrowsForeignKeyViolation()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await InsertProductAsync(conn, id, isPublished: true);
        await AuthorizeAsAsync(conn, "brands.delete");

        var act = () => DeleteBrandRowAsync(conn, id);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23503");
    }

    // =========================================================================
    // brand_product_counts(uuid[]) — authoritative counts, published or not (RULE-6, Decision 3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BrandProductCounts_CountsUnpublishedProductsToo()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await InsertProductAsync(conn, id, isPublished: false);
        await AuthorizeAsAsync(conn, "brands.view");

        var counts = await CallBrandProductCountsAsync(conn, [id]);

        counts[id].Should().Be(1, "a brand used only by discontinued products still counts against deletion (RULE-6)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BrandProductCounts_WhenBrandHasNoProducts_ReturnsZero()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, "brands.view");

        var counts = await CallBrandProductCountsAsync(conn, [id]);

        counts[id].Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BrandProductCounts_ForMultipleBrands_ReturnsTheCountPerBrand()
    {
        await using var conn = await OpenAsync();
        var busyId = await InsertBrandAsync(conn, "Elf Bar");
        await InsertProductAsync(conn, busyId, isPublished: true);
        await InsertProductAsync(conn, busyId, isPublished: true);
        var idleId = await InsertBrandAsync(conn, "Lost Mary");
        await AuthorizeAsAsync(conn, "brands.view");

        var counts = await CallBrandProductCountsAsync(conn, [busyId, idleId]);

        counts[busyId].Should().Be(2);
        counts[idleId].Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task BrandProductCounts_WhenCallerLacksBrandsViewPermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallBrandProductCountsAsync(conn, [id]);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // delete_brands(uuid[]) — atomic partial-success delete (RULE-6, RULE-13, Decision 2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WhenTheBrandHasNoProducts_DeletesItAndReportsDeletedTrue()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, "brands.delete");

        var rows = await CallDeleteBrandsAsync(conn, [id]);

        rows.Should().ContainSingle(r => r.Id == id && r.Deleted);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WhenTheBrandHasNoProducts_RemovesTheRowFromTheTable()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, "brands.delete");

        await CallDeleteBrandsAsync(conn, [id]);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM brands WHERE id = '{id}'");
        Convert.ToInt32(count).Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WhenTheBrandHasAProduct_IncludingDiscontinued_KeepsItAndReportsItsProductCount()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await InsertProductAsync(conn, id, isPublished: false); // discontinued still counts (RULE-6)
        await AuthorizeAsAsync(conn, "brands.delete");

        var rows = await CallDeleteBrandsAsync(conn, [id]);

        rows.Should().ContainSingle(r => r.Id == id && !r.Deleted && r.ProductCount == 1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_ReturnsTheDeletedBrandsLogoPathForDisposal()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar", logoPath: "brands/abc/logo.webp");
        await AuthorizeAsAsync(conn, "brands.delete");

        var rows = await CallDeleteBrandsAsync(conn, [id]);

        rows.Single(r => r.Id == id).LogoPath.Should().Be("brands/abc/logo.webp");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WithAMixOfUnusedAndInUseBrands_DeletesTheUnusedAndKeepsTheUsedInOneCall()
    {
        // RULE-13: never all-or-nothing — one in-use brand does not block the rest.
        await using var conn = await OpenAsync();
        var unusedId = await InsertBrandAsync(conn, "Elf Bar");
        var usedId = await InsertBrandAsync(conn, "Lost Mary");
        await InsertProductAsync(conn, usedId, isPublished: true);
        await AuthorizeAsAsync(conn, "brands.delete");

        var rows = await CallDeleteBrandsAsync(conn, [unusedId, usedId]);

        rows.Should().ContainSingle(r => r.Id == unusedId && r.Deleted);
        rows.Should().ContainSingle(r => r.Id == usedId && !r.Deleted && r.ProductCount == 1);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WhenEverySelectedBrandIsInUse_DeletesNone()
    {
        await using var conn = await OpenAsync();
        var firstId = await InsertBrandAsync(conn, "Elf Bar");
        var secondId = await InsertBrandAsync(conn, "Lost Mary");
        await InsertProductAsync(conn, firstId, isPublished: true);
        await InsertProductAsync(conn, secondId, isPublished: true);
        await AuthorizeAsAsync(conn, "brands.delete");

        var rows = await CallDeleteBrandsAsync(conn, [firstId, secondId]);

        rows.Should().OnlyContain(r => !r.Deleted);
        var count = await ScalarAsync(conn, "SELECT COUNT(*) FROM brands");
        Convert.ToInt32(count).Should().Be(2, "nothing may be deleted when every selected brand is in use (RULE-13)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public async Task DeleteBrands_WhenCallerLacksBrandsDeletePermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        var id = await InsertBrandAsync(conn, "Elf Bar");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallDeleteBrandsAsync(conn, [id]);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    /// <summary>Grants the caller a fresh Admin-rooted session limited to one permission (or none).</summary>
    private static async Task AuthorizeAsAsync(NpgsqlConnection conn, string? permissionCode)
    {
        var userId = await RbacTestSchema.InsertAuthUserAsync(conn);
        if (permissionCode is not null)
        {
            await RbacTestSchema.AssignRoleByNameAsync(conn, userId, "Admin");
        }
        var sessionId = await RbacTestSchema.InsertSessionAsync(conn, userId, DateTimeOffset.UtcNow);
        await RbacTestSchema.SetJwtClaimsAsync(conn, userId, sessionId);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SET ROLE authenticated";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertBrandAsync(NpgsqlConnection conn, string name, string? logoPath = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = logoPath is null
            ? $"INSERT INTO brands (name) VALUES ('{name.Replace("'", "''")}') RETURNING id"
            : $"INSERT INTO brands (name, logo_path) VALUES ('{name.Replace("'", "''")}', '{logoPath}') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<int> UpdateBrandNameAsync(NpgsqlConnection conn, Guid id, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE brands SET name = '{name.Replace("'", "''")}' WHERE id = '{id}'";
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> DeleteBrandRowAsync(NpgsqlConnection conn, Guid id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM brands WHERE id = '{id}'";
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProductAsync(NpgsqlConnection conn, Guid brandId, bool isPublished)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products (brand_id, is_published) VALUES ('{brandId}', {(isPublished ? "true" : "false")})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Dictionary<Guid, long>> CallBrandProductCountsAsync(NpgsqlConnection conn, IEnumerable<Guid> ids)
    {
        var idArray = string.Join(",", ids.Select(id => $"'{id}'"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT brand_id, product_count FROM public.brand_product_counts(ARRAY[{idArray}]::uuid[])";

        var result = new Dictionary<Guid, long>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetGuid(0)] = reader.GetInt64(1);
        return result;
    }

    private sealed record DeleteBrandsRow(Guid Id, string Name, string? LogoPath, long ProductCount, bool Deleted);

    private static async Task<List<DeleteBrandsRow>> CallDeleteBrandsAsync(NpgsqlConnection conn, IEnumerable<Guid> ids)
    {
        var idArray = string.Join(",", ids.Select(id => $"'{id}'"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, name, logo_path, product_count, deleted FROM public.delete_brands(ARRAY[{idArray}]::uuid[])";

        var rows = new List<DeleteBrandsRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new DeleteBrandsRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt64(3),
                reader.GetBoolean(4)));
        }
        return rows;
    }

    // =========================================================================
    // Schema setup — the manage-brands deltas layered on RbacTestSchema's RBAC core (plan §10):
    // brands minus slug, idx_brands_name_lower, ux_brands_normalized_name, a minimal products
    // table (brand_id FK + is_published) with no SELECT policy of its own (irrelevant to the
    // SECURITY DEFINER RPCs below, which bypass RLS by design), the four brands.* RLS policies,
    // and the two RPCs verbatim from migration 0015_manage_brands.sql.
    // =========================================================================

    private static async Task ApplyManageBrandsSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE brands (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name        TEXT NOT NULL,
                description TEXT,
                logo_path   TEXT,
                is_active   BOOLEAN NOT NULL DEFAULT TRUE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX ux_brands_normalized_name ON brands (lower(btrim(name)));
            CREATE INDEX idx_brands_name_lower ON brands (lower(name));

            CREATE TABLE products (
                id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                brand_id     UUID NOT NULL REFERENCES brands(id),
                is_published BOOLEAN NOT NULL DEFAULT TRUE
            );

            ALTER TABLE brands   ENABLE ROW LEVEL SECURITY;
            ALTER TABLE products ENABLE ROW LEVEL SECURITY;

            CREATE POLICY "brands_read" ON brands
                FOR SELECT USING (is_active OR (SELECT public.authorize('brands.view')));

            CREATE POLICY "brands_admin_insert" ON brands
                FOR INSERT WITH CHECK ((SELECT public.authorize('brands.create')));

            CREATE POLICY "brands_admin_update" ON brands
                FOR UPDATE
                USING ((SELECT public.authorize('brands.edit')))
                WITH CHECK ((SELECT public.authorize('brands.edit')));

            CREATE POLICY "brands_admin_delete" ON brands
                FOR DELETE USING ((SELECT public.authorize('brands.delete')));

            CREATE POLICY "products_public_read" ON products FOR SELECT USING (is_published = true);

            -- brands.* permissions and the Admin grant are seeded by RbacTestSchema
            -- (mirroring migrations 0007 + 0015).

            -- Verbatim from supabase/migrations/0015_manage_brands.sql (plan §10)

            CREATE OR REPLACE FUNCTION public.brand_product_counts(brand_ids UUID[])
            RETURNS TABLE (brand_id UUID, product_count BIGINT)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            BEGIN
                IF NOT public.authorize('brands.view') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                SELECT b.id, count(p.id)
                FROM brands b
                LEFT JOIN products p ON p.brand_id = b.id
                WHERE b.id = ANY(brand_ids)
                GROUP BY b.id;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.delete_brands(brand_ids UUID[])
            RETURNS TABLE (id UUID, name TEXT, logo_path TEXT, product_count BIGINT, deleted BOOLEAN)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            BEGIN
                IF NOT public.authorize('brands.delete') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                WITH candidates AS (
                    SELECT b.id, b.name, b.logo_path,
                           (SELECT count(*) FROM products p WHERE p.brand_id = b.id) AS product_count
                    FROM brands b
                    WHERE b.id = ANY(brand_ids)
                ),
                removed AS (
                    DELETE FROM brands
                    WHERE brands.id IN (SELECT c.id FROM candidates c WHERE c.product_count = 0)
                    RETURNING brands.id
                )
                SELECT c.id, c.name, c.logo_path, c.product_count,
                       EXISTS (SELECT 1 FROM removed r WHERE r.id = c.id)
                FROM candidates c;
            END;
            $$;

            REVOKE ALL ON FUNCTION public.brand_product_counts(UUID[]) FROM PUBLIC, anon;
            REVOKE ALL ON FUNCTION public.delete_brands(UUID[])        FROM PUBLIC, anon;
            GRANT EXECUTE ON FUNCTION public.brand_product_counts(UUID[]) TO authenticated;
            GRANT EXECUTE ON FUNCTION public.delete_brands(UUID[])        TO authenticated;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-13: DeleteBrands_WhenTheBrandHasNoProducts_DeletesItAndReportsDeletedTrue,
//         DeleteBrands_WhenTheBrandHasNoProducts_RemovesTheRowFromTheTable,
//         DeleteBrands_ReturnsTheDeletedBrandsLogoPathForDisposal
// AC-15: DeleteBrands_WhenTheBrandHasAProduct_IncludingDiscontinued_KeepsItAndReportsItsProductCount
// AC-24: DeleteBrands_WithAMixOfUnusedAndInUseBrands_DeletesTheUnusedAndKeepsTheUsedInOneCall,
//         BrandProductCounts_ForMultipleBrands_ReturnsTheCountPerBrand
// AC-25: DeleteBrands_WhenEverySelectedBrandIsInUse_DeletesNone
// AC-26: DeleteBrands_WhenCallerLacksBrandsDeletePermission_ThrowsAccessDeniedException
// AC-28: UpdateBrand_WhenRenamedToADifferentPunctuationVariantOfAnotherBrandsName_Succeeds,
//         Schema_BrandsTable_HasNoSlugColumn
// (RULE-8 storage backstop: UpdateBrand_WhenCallerLacksBrandsEditPermission_TheUpdateAffectsNoRows,
//  DeleteBrandRow_WhenCallerLacksBrandsDeletePermission_TheDeleteAffectsNoRows,
//  BrandProductCounts_WhenCallerLacksBrandsViewPermission_ThrowsAccessDeniedException)
// (RULE-6 last-resort FK guard: DeleteBrandRow_WhenReferencedByAProduct_ThrowsForeignKeyViolation,
//  BrandProductCounts_CountsUnpublishedProductsToo)
