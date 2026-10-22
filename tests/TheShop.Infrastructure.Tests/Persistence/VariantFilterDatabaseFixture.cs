using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class VariantFilterDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithDatabase("variant_filters").WithUsername("shop").WithPassword("shop").Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(conn);
        await RbacTestSchema.ApplyRbacCoreAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await ExecuteAsync(conn, """
            CREATE TABLE brands (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name text NOT NULL, is_active boolean NOT NULL DEFAULT true);
            CREATE TABLE categories (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name text NOT NULL, is_active boolean NOT NULL DEFAULT true);
            CREATE TABLE products (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(), name text NOT NULL, sku text NOT NULL DEFAULT 'SKU',
                description text NOT NULL DEFAULT '', image_path text, currency text NOT NULL DEFAULT 'CAD',
                brand_id uuid NOT NULL REFERENCES brands(id), category_id uuid NOT NULL REFERENCES categories(id),
                original_price numeric, sale_price numeric, min_variant_price numeric, has_sellable_variant boolean DEFAULT true,
                display_price numeric GENERATED ALWAYS AS (coalesce(sale_price, original_price, min_variant_price)) STORED,
                is_published boolean NOT NULL DEFAULT true, created_at timestamptz NOT NULL DEFAULT '2026-01-01Z',
                updated_at timestamptz NOT NULL DEFAULT '2026-01-01Z');
            ALTER TABLE products ENABLE ROW LEVEL SECURITY;
            CREATE POLICY products_public_read ON products FOR SELECT USING (is_published);
            CREATE POLICY products_admin_read ON products FOR SELECT USING ((SELECT public.authorize('products.view')));
            """);
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0022_create_product_variants.sql")));
        // Grant prerequisites first. Never override the feature migration's function grants.
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0028_manage_products.sql")));
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0033_variant_filters.sql")));
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    public static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string Migration(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "supabase", "migrations")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("supabase/migrations"), "supabase", "migrations", file);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();
}
