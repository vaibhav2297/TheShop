using FluentAssertions;
using System.Text.Json;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class VariantUrlGallerySchemaTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithDatabase("variant_gallery")
        .WithUsername("shop").WithPassword("shop").Build();
    private readonly Guid _published = Guid.NewGuid();
    private readonly Guid _unpublished = Guid.NewGuid();
    private readonly Guid _firstImage = Guid.NewGuid();
    private readonly Guid _firstVariant = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var connection = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(connection);
        await RbacTestSchema.ApplyRbacCoreAsync(connection);
        await RbacTestSchema.CreateRlsTestRolesAsync(connection);
        await ExecuteAsync(connection, """
            CREATE TABLE products (id uuid PRIMARY KEY, name text NOT NULL, is_published boolean NOT NULL);
            ALTER TABLE products ENABLE ROW LEVEL SECURITY;
            CREATE POLICY products_public_read ON products FOR SELECT USING (is_published);
            CREATE POLICY products_admin_read ON products FOR SELECT USING ((SELECT public.authorize('products.view')));
            CREATE FUNCTION public.save_product(payload jsonb)
            RETURNS TABLE(id uuid, updated_at timestamptz)
            LANGUAGE sql SECURITY DEFINER SET search_path = public AS $$
              SELECT (payload->'product'->>'id')::uuid, now();
            $$;
            """);
        await ExecuteAsync(connection, File.ReadAllText(Migration("0022_create_product_variants.sql")));
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(connection);
        await using (var seed = new NpgsqlCommand("""
            INSERT INTO products(id, name, is_published) VALUES
                (@published, 'Peach Ice', true), (@unpublished, 'Secret', false);
            INSERT INTO product_images(id, product_id, object_key, position, is_primary)
                VALUES (@image, @published, 'peach.png', 0, true);
            INSERT INTO product_option_types(id, product_id, name, position)
                VALUES ('00000000-0000-0000-0000-000000000001', @published, 'Flavour', 0);
            INSERT INTO product_option_values(id, option_type_id, value, position)
                VALUES ('00000000-0000-0000-0000-000000000002',
                    '00000000-0000-0000-0000-000000000001', 'Peach', 0);
            INSERT INTO product_variants(id, product_id, sku, original_price, image_id, position)
                VALUES (@variant, @published, 'PEACH', 20, @image, 0);
            INSERT INTO product_variant_option_values(variant_id, option_value_id)
                VALUES (@variant, '00000000-0000-0000-0000-000000000002');
            """, connection))
        {
            seed.Parameters.AddWithValue("published", _published);
            seed.Parameters.AddWithValue("unpublished", _unpublished);
            seed.Parameters.AddWithValue("image", _firstImage);
            seed.Parameters.AddWithValue("variant", _firstVariant);
            await seed.ExecuteNonQueryAsync();
        }
        await ExecuteAsync(connection, File.ReadAllText(Migration("0039_variant_urls_and_galleries.sql")));
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Migration_BackfillsPinAndStableReadableRoutes()
    {
        await using var connection = await OpenAsync();
        await using var query = new NpgsqlCommand("""
            SELECT r.slug, vr.slug, vi.image_id, vi.position
            FROM product_routes r
            JOIN product_variant_routes vr ON vr.product_slug = r.slug
            JOIN product_variant_images vi ON vi.variant_id = vr.variant_id
            WHERE r.product_id = @product;
            """, connection);
        query.Parameters.AddWithValue("product", _published);
        await using var reader = await query.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("peach-ice");
        reader.GetString(1).Should().Be("peach");
        reader.GetGuid(2).Should().Be(_firstImage);
        reader.GetInt32(3).Should().Be(0);
        (await reader.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task PublicRead_HidesUnpublishedRoutes()
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, "SET ROLE anon");
        await using var query = new NpgsqlCommand("SELECT slug FROM product_routes ORDER BY slug", connection);
        await using var reader = await query.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("peach-ice");
        (await reader.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task SaveProduct_GuestCannotChangeAssignments()
    {
        await using var connection = await OpenAsync();
        await ExecuteAsync(connection, "SET ROLE authenticated");
        await using var command = new NpgsqlCommand("SELECT * FROM save_product(@payload::jsonb)", connection);
        command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(new { mode = "update", product = new { id = _published } }));

        var act = () => command.ExecuteNonQueryAsync();

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "007_variant-urls-and-galleries")]
    public async Task Routes_RenameKeepsSavedSlug_AndCollisionGetsSuffix()
    {
        var collision = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await using (var change = new NpgsqlCommand("""
            UPDATE products SET name = 'New Display Name' WHERE id = @published;
            UPDATE product_option_values SET value = 'New Flavour'
                WHERE id = '00000000-0000-0000-0000-000000000002';
            INSERT INTO products(id, name, is_published) VALUES (@collision, 'Peach Ice', true);
            SELECT ensure_product_routes_007(@published);
            SELECT ensure_product_routes_007(@collision);
            """, connection))
        {
            change.Parameters.AddWithValue("published", _published);
            change.Parameters.AddWithValue("collision", collision);
            await change.ExecuteNonQueryAsync();
        }
        await using var query = new NpgsqlCommand("""
            SELECT r.slug, vr.slug FROM product_routes r
            LEFT JOIN product_variant_routes vr ON vr.product_slug = r.slug
            WHERE r.product_id = @published;
            """, connection);
        query.Parameters.AddWithValue("published", _published);
        await using (var reader = await query.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("peach-ice");
            reader.GetString(1).Should().Be("peach");
        }
        await using var collisionQuery = new NpgsqlCommand(
            "SELECT slug FROM product_routes WHERE product_id = @collision", connection);
        collisionQuery.Parameters.AddWithValue("collision", collision);
        (await collisionQuery.ExecuteScalarAsync()).Should().Be("peach-ice-2");
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string Migration(string filename)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TheShop.slnx")))
                return Path.Combine(directory.FullName, "supabase", "migrations", filename);
        throw new DirectoryNotFoundException("TheShop.slnx not found");
    }
}
