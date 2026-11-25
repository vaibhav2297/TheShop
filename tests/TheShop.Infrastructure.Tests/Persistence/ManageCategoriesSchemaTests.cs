using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the manage-categories schema deltas (.specs/manage-categories/plan.md
/// §10): the dropped <c>categories.slug</c> column and its unique index (spec FR-11, Decision 6,
/// AC-24), the <c>category_product_counts(uuid[])</c> and <c>delete_categories(uuid[])</c>
/// <c>SECURITY DEFINER</c> RPCs that answer RULE-6/RULE-15 questions no client-visible RLS policy
/// can (Decisions 2/3), and the three <c>categories_admin_*</c> write policies (RULE-8 — the 0019
/// lesson: an absent UPDATE/DELETE policy makes PostgREST return 200 OK having matched zero rows).
/// Exercised in production by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseCategoryRepository"/>.
///
/// Spins up a real Postgres container (Testcontainers), reproduces the RBAC core via
/// <see cref="RbacTestSchema"/> (the same mechanism <see cref="SupabaseCategoryRepositorySchemaTests"/>
/// uses), then layers a minimal <c>categories</c>/<c>products</c> pair on top — the slug-free,
/// post-migration-0020 shape of <c>categories</c> plus just enough of <c>products</c>
/// (<c>category_id</c>, <c>is_published</c>) for the two RPCs to answer against. RULE-2's
/// uniqueness backstop and the <c>categories_public_read</c>/<c>categories_admin_insert</c>
/// policies are already covered by <see cref="SupabaseCategoryRepositorySchemaTests"/>; this class
/// covers only what manage-categories adds or changes.
/// <see href=".specs/manage-categories/spec.md"/>
/// <see href=".specs/manage-categories/plan.md"/>
/// </summary>
public sealed class ManageCategoriesSchemaTests : IAsyncLifetime
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
        await ApplyManageCategoriesSchemaAsync(conn);
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
    // (spec FR-11, Decision 6, AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Schema_CategoriesTable_HasNoSlugColumn()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn,
            "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'categories' AND column_name = 'slug'");

        Convert.ToInt32(count).Should().Be(0, "the category's name-derived identifier is retired (FR-11)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Schema_IdxCategoriesNameLower_Exists()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn,
            "SELECT COUNT(*) FROM pg_indexes WHERE indexname = 'idx_categories_name_lower'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task Schema_IdxCategoriesCreatedAt_Exists()
    {
        await using var conn = await OpenAsync();

        var count = await ScalarAsync(conn,
            "SELECT COUNT(*) FROM pg_indexes WHERE indexname = 'idx_categories_created_at'");

        Convert.ToInt32(count).Should().Be(1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task UpdateCategory_WhenRenamedToADifferentPunctuationVariantOfAnotherCategorysName_Succeeds()
    {
        // AC-24: "Vape Kits" and "Vape.Kits" are distinct under RULE-2 — with the slug (and its
        // separate normalization) gone, only ux_categories_name_normalized governs uniqueness, and
        // it does not strip punctuation.
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Vape Kits");
        var otherId = await InsertCategoryAsync(conn, "Other Category");

        var act = () => UpdateCategoryNameAsync(conn, otherId, "Vape.Kits");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task UpdateCategory_WhenRenamedToAnotherCategorysNameDifferingOnlyByCapitalization_ThrowsUniqueViolation()
    {
        // RULE-2 via the UPDATE path — the edit form's persistence route, not add's INSERT.
        await using var conn = await OpenAsync();
        await InsertCategoryAsync(conn, "Disposables");
        var otherId = await InsertCategoryAsync(conn, "Pod Systems");

        var act = () => UpdateCategoryNameAsync(conn, otherId, "DISPOSABLES");

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task UpdateCategory_SavedWithItsOwnUnchangedName_Succeeds()
    {
        // AC-11: a no-op save must not collide with itself. The application layer achieves this via
        // ExistsByNormalizedNameAsync(excludeCategoryId); at the storage level, updating a row to
        // the name it already holds is simply not a duplicate of any *other* row.
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");

        var act = () => UpdateCategoryNameAsync(conn, id, "Disposables");

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // categories_admin_update / categories_admin_delete RLS (RULE-8, the 0019 lesson)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task UpdateCategory_WhenCallerHoldsCategoriesEditPermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, "categories.edit");

        var act = () => UpdateCategoryNameAsync(conn, id, "Pod Systems");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task UpdateCategory_WhenCallerLacksCategoriesEditPermission_TheUpdateAffectsNoRows()
    {
        // Unlike an INSERT's WITH CHECK, a USING-only denial on UPDATE never raises — the
        // categories_admin_update policy just hides the row from the WHERE clause, so the
        // statement silently matches nothing rather than throwing 42501 (RULE-8, the 0019 lesson:
        // a missing/wrong policy makes PostgREST return 200 OK having matched zero rows).
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var affected = await UpdateCategoryNameAsync(conn, id, "Pod Systems");

        affected.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategoryRow_WhenCallerHoldsCategoriesDeletePermission_Succeeds()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, "categories.delete");

        var act = () => DeleteCategoryRowAsync(conn, id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategoryRow_WhenCallerLacksCategoriesDeletePermission_TheDeleteAffectsNoRows()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var affected = await DeleteCategoryRowAsync(conn, id);

        affected.Should().Be(0);
    }

    // =========================================================================
    // products.category_id FK — the last-resort guard behind RULE-6, even for a caller who holds
    // categories.delete (the RPC below is what stands between staff and this raw violation)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategoryRow_WhenReferencedByAProduct_ThrowsForeignKeyViolation()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await InsertProductAsync(conn, id, isPublished: true);
        await AuthorizeAsAsync(conn, "categories.delete");

        var act = () => DeleteCategoryRowAsync(conn, id);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23503");
    }

    // =========================================================================
    // category_product_counts(uuid[]) — authoritative counts, published or not (RULE-6, Decision 3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryProductCounts_CountsUnpublishedProductsToo()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await InsertProductAsync(conn, id, isPublished: false);
        await AuthorizeAsAsync(conn, "categories.view");

        var counts = await CallCategoryProductCountsAsync(conn, [id]);

        counts[id].Should().Be(1, "a category used only by discontinued products still counts against deletion (RULE-6)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryProductCounts_WhenCategoryHasNoProducts_ReturnsZero()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, "categories.view");

        var counts = await CallCategoryProductCountsAsync(conn, [id]);

        counts[id].Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryProductCounts_ForMultipleCategories_ReturnsTheCountPerCategory()
    {
        await using var conn = await OpenAsync();
        var busyId = await InsertCategoryAsync(conn, "Disposables");
        await InsertProductAsync(conn, busyId, isPublished: true);
        await InsertProductAsync(conn, busyId, isPublished: true);
        var idleId = await InsertCategoryAsync(conn, "Pod Systems");
        await AuthorizeAsAsync(conn, "categories.view");

        var counts = await CallCategoryProductCountsAsync(conn, [busyId, idleId]);

        counts[busyId].Should().Be(2);
        counts[idleId].Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task CategoryProductCounts_WhenCallerLacksCategoriesViewPermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallCategoryProductCountsAsync(conn, [id]);

        await act.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    // =========================================================================
    // delete_categories(uuid[]) — atomic partial-success delete (RULE-6, RULE-15, Decision 2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WhenTheCategoryHasNoProducts_DeletesItAndReportsDeletedTrue()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, "categories.delete");

        var rows = await CallDeleteCategoriesAsync(conn, [id]);

        rows.Should().ContainSingle(r => r.Id == id && r.Deleted);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WhenTheCategoryHasNoProducts_RemovesTheRowFromTheTable()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, "categories.delete");

        await CallDeleteCategoriesAsync(conn, [id]);

        var count = await ScalarAsync(conn, $"SELECT COUNT(*) FROM categories WHERE id = '{id}'");
        Convert.ToInt32(count).Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WhenTheCategoryHasAProduct_IncludingDiscontinued_KeepsItAndReportsItsProductCount()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await InsertProductAsync(conn, id, isPublished: false); // discontinued still counts (RULE-6)
        await AuthorizeAsAsync(conn, "categories.delete");

        var rows = await CallDeleteCategoriesAsync(conn, [id]);

        rows.Should().ContainSingle(r => r.Id == id && !r.Deleted && r.ProductCount == 1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_ReturnsTheDeletedCategorysImagePathForDisposal()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables", imagePath: "categories/abc/image.webp");
        await AuthorizeAsAsync(conn, "categories.delete");

        var rows = await CallDeleteCategoriesAsync(conn, [id]);

        rows.Single(r => r.Id == id).ImagePath.Should().Be("categories/abc/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WithAMixOfUnusedAndInUseCategories_DeletesTheUnusedAndKeepsTheUsedInOneCall()
    {
        // RULE-15: never all-or-nothing — one in-use category does not block the rest.
        await using var conn = await OpenAsync();
        var unusedId = await InsertCategoryAsync(conn, "Disposables");
        var usedId = await InsertCategoryAsync(conn, "Pod Systems");
        await InsertProductAsync(conn, usedId, isPublished: true);
        await AuthorizeAsAsync(conn, "categories.delete");

        var rows = await CallDeleteCategoriesAsync(conn, [unusedId, usedId]);

        rows.Should().ContainSingle(r => r.Id == unusedId && r.Deleted);
        rows.Should().ContainSingle(r => r.Id == usedId && !r.Deleted && r.ProductCount == 1);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WhenEverySelectedCategoryIsInUse_DeletesNone()
    {
        await using var conn = await OpenAsync();
        var firstId = await InsertCategoryAsync(conn, "Disposables");
        var secondId = await InsertCategoryAsync(conn, "Pod Systems");
        await InsertProductAsync(conn, firstId, isPublished: true);
        await InsertProductAsync(conn, secondId, isPublished: true);
        await AuthorizeAsAsync(conn, "categories.delete");

        var rows = await CallDeleteCategoriesAsync(conn, [firstId, secondId]);

        rows.Should().OnlyContain(r => !r.Deleted);
        var count = await ScalarAsync(conn, "SELECT COUNT(*) FROM categories");
        Convert.ToInt32(count).Should().Be(2, "nothing may be deleted when every selected category is in use (RULE-15)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public async Task DeleteCategories_WhenCallerLacksCategoriesDeletePermission_ThrowsAccessDeniedException()
    {
        await using var conn = await OpenAsync();
        var id = await InsertCategoryAsync(conn, "Disposables");
        await AuthorizeAsAsync(conn, permissionCode: null);

        var act = () => CallDeleteCategoriesAsync(conn, [id]);

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

    private static async Task<Guid> InsertCategoryAsync(NpgsqlConnection conn, string name, string? imagePath = null)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = imagePath is null
            ? $"INSERT INTO categories (name) VALUES ('{name.Replace("'", "''")}') RETURNING id"
            : $"INSERT INTO categories (name, image_path) VALUES ('{name.Replace("'", "''")}', '{imagePath}') RETURNING id";
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<int> UpdateCategoryNameAsync(NpgsqlConnection conn, Guid id, string name)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE categories SET name = '{name.Replace("'", "''")}' WHERE id = '{id}'";
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> DeleteCategoryRowAsync(NpgsqlConnection conn, Guid id)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM categories WHERE id = '{id}'";
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Guid> InsertProductAsync(NpgsqlConnection conn, Guid categoryId, bool isPublished)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO products (category_id, is_published) VALUES ('{categoryId}', {(isPublished ? "true" : "false")})
            RETURNING id
            """;
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<Dictionary<Guid, long>> CallCategoryProductCountsAsync(NpgsqlConnection conn, IEnumerable<Guid> ids)
    {
        var idArray = string.Join(",", ids.Select(id => $"'{id}'"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT category_id, product_count FROM public.category_product_counts(ARRAY[{idArray}]::uuid[])";

        var result = new Dictionary<Guid, long>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetGuid(0)] = reader.GetInt64(1);
        return result;
    }

    private sealed record DeleteCategoriesRow(Guid Id, string Name, string? ImagePath, long ProductCount, bool Deleted);

    private static async Task<List<DeleteCategoriesRow>> CallDeleteCategoriesAsync(NpgsqlConnection conn, IEnumerable<Guid> ids)
    {
        var idArray = string.Join(",", ids.Select(id => $"'{id}'"));
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, name, image_path, product_count, deleted FROM public.delete_categories(ARRAY[{idArray}]::uuid[])";

        var rows = new List<DeleteCategoriesRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new DeleteCategoriesRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt64(3),
                reader.GetBoolean(4)));
        }
        return rows;
    }

    // =========================================================================
    // Schema setup — the manage-categories deltas layered on RbacTestSchema's RBAC core (plan §10):
    // categories minus slug, idx_categories_name_lower, idx_categories_created_at,
    // ux_categories_name_normalized, a minimal products table (category_id FK + is_published)
    // with no SELECT policy of its own (irrelevant to the SECURITY DEFINER RPCs below, which
    // bypass RLS by design), the three categories.* write RLS policies, and the two RPCs verbatim
    // from migration 0020_manage_categories.sql.
    // =========================================================================

    private static async Task ApplyManageCategoriesSchemaAsync(NpgsqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE categories (
                id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                name        TEXT NOT NULL,
                description TEXT,
                image_path  TEXT,
                is_active   BOOLEAN NOT NULL DEFAULT TRUE,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX ux_categories_name_normalized ON categories (lower(btrim(name)));
            CREATE INDEX idx_categories_name_lower ON categories (lower(name));
            CREATE INDEX idx_categories_created_at ON categories (created_at DESC);

            CREATE TABLE products (
                id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                category_id  UUID NOT NULL REFERENCES categories(id),
                is_published BOOLEAN NOT NULL DEFAULT TRUE
            );

            ALTER TABLE categories ENABLE ROW LEVEL SECURITY;
            ALTER TABLE products   ENABLE ROW LEVEL SECURITY;

            -- SELECT is deliberately USING (true) — Decision 2 (.specs/manage-categories/plan.md
            -- §5): restricting it to is_active would null out ProductRecord's embedded
            -- CategoryRecord for every product in a deactivated category and break the catalogue.
            CREATE POLICY "categories_public_read" ON categories FOR SELECT USING (true);

            CREATE POLICY "categories_admin_insert" ON categories
                FOR INSERT WITH CHECK ((SELECT public.authorize('categories.create')));

            CREATE POLICY "categories_admin_update" ON categories
                FOR UPDATE
                USING ((SELECT public.authorize('categories.edit')))
                WITH CHECK ((SELECT public.authorize('categories.edit')));

            CREATE POLICY "categories_admin_delete" ON categories
                FOR DELETE USING ((SELECT public.authorize('categories.delete')));

            CREATE POLICY "products_public_read" ON products FOR SELECT USING (is_published = true);

            -- categories.* permissions and the Admin grant are seeded by RbacTestSchema
            -- (mirroring migrations 0007 + 0020).

            -- Verbatim from supabase/migrations/0020_manage_categories.sql (plan §10)

            CREATE OR REPLACE FUNCTION public.category_product_counts(category_ids UUID[])
            RETURNS TABLE (category_id UUID, product_count BIGINT)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            BEGIN
                IF NOT public.authorize('categories.view') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                SELECT c.id, count(p.id)
                FROM categories c
                LEFT JOIN products p ON p.category_id = c.id
                WHERE c.id = ANY(category_ids)
                GROUP BY c.id;
            END;
            $$;

            CREATE OR REPLACE FUNCTION public.delete_categories(category_ids UUID[])
            RETURNS TABLE (id UUID, name TEXT, image_path TEXT, product_count BIGINT, deleted BOOLEAN)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
            BEGIN
                IF NOT public.authorize('categories.delete') THEN
                    RAISE EXCEPTION 'access denied' USING ERRCODE = 'insufficient_privilege';
                END IF;

                RETURN QUERY
                WITH candidates AS (
                    SELECT c.id, c.name, c.image_path,
                           (SELECT count(*) FROM products p WHERE p.category_id = c.id) AS product_count
                    FROM categories c
                    WHERE c.id = ANY(category_ids)
                ),
                removed AS (
                    DELETE FROM categories
                    WHERE categories.id IN (SELECT k.id FROM candidates k WHERE k.product_count = 0)
                    RETURNING categories.id
                )
                SELECT k.id, k.name, k.image_path, k.product_count,
                       EXISTS (SELECT 1 FROM removed r WHERE r.id = k.id)
                FROM candidates k;
            END;
            $$;

            REVOKE ALL ON FUNCTION public.category_product_counts(UUID[]) FROM PUBLIC, anon;
            REVOKE ALL ON FUNCTION public.delete_categories(UUID[])       FROM PUBLIC, anon;
            GRANT EXECUTE ON FUNCTION public.category_product_counts(UUID[]) TO authenticated;
            GRANT EXECUTE ON FUNCTION public.delete_categories(UUID[])       TO authenticated;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-17: DeleteCategories_WhenTheCategoryHasNoProducts_DeletesItAndReportsDeletedTrue,
//         DeleteCategories_WhenTheCategoryHasNoProducts_RemovesTheRowFromTheTable,
//         DeleteCategories_ReturnsTheDeletedCategorysImagePathForDisposal
// AC-19: DeleteCategories_WhenTheCategoryHasAProduct_IncludingDiscontinued_KeepsItAndReportsItsProductCount
// AC-24: UpdateCategory_WhenRenamedToADifferentPunctuationVariantOfAnotherCategorysName_Succeeds,
//         Schema_CategoriesTable_HasNoSlugColumn
// AC-29: DeleteCategories_WithAMixOfUnusedAndInUseCategories_DeletesTheUnusedAndKeepsTheUsedInOneCall,
//         CategoryProductCounts_ForMultipleCategories_ReturnsTheCountPerCategory
// AC-30: DeleteCategories_WhenEverySelectedCategoryIsInUse_DeletesNone
// AC-31: DeleteCategories_WhenCallerLacksCategoriesDeletePermission_ThrowsAccessDeniedException
// AC-32: Schema_IdxCategoriesCreatedAt_Exists
// (RULE-8 storage backstop / the 0019 lesson: UpdateCategory_WhenCallerLacksCategoriesEditPermission_TheUpdateAffectsNoRows,
//  DeleteCategoryRow_WhenCallerLacksCategoriesDeletePermission_TheDeleteAffectsNoRows,
//  CategoryProductCounts_WhenCallerLacksCategoriesViewPermission_ThrowsAccessDeniedException)
// (RULE-6 last-resort FK guard: DeleteCategoryRow_WhenReferencedByAProduct_ThrowsForeignKeyViolation,
//  CategoryProductCounts_CountsUnpublishedProductsToo)
