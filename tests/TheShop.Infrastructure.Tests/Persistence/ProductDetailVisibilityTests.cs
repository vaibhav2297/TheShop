using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class ProductDetailVisibilityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("product_detail_test").WithUsername("shop").WithPassword("shop").Build();
    private Guid _customer, _staff, _customerSession, _staffSession;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var connection = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(connection);
        await RbacTestSchema.ApplyRbacCoreAsync(connection);
        await RbacTestSchema.CreateRlsTestRolesAsync(connection);
        _customer = await RbacTestSchema.InsertAuthUserAsync(connection, "customer@example.com");
        _staff = await RbacTestSchema.InsertAuthUserAsync(connection, "staff@example.com");
        _customerSession = await RbacTestSchema.InsertSessionAsync(connection, _customer, DateTimeOffset.Parse("2026-10-20T00:00:00Z"));
        _staffSession = await RbacTestSchema.InsertSessionAsync(connection, _staff, DateTimeOffset.Parse("2026-10-20T00:00:00Z"));
        await RbacTestSchema.AssignRoleByNameAsync(connection, _staff, "Admin");
        await ExecuteAsync(connection, """
            CREATE TABLE products (id UUID PRIMARY KEY, is_published BOOLEAN NOT NULL);
            ALTER TABLE products ENABLE ROW LEVEL SECURITY;
            CREATE POLICY products_public_read ON products FOR SELECT USING (is_published = true);
            CREATE POLICY products_admin_read ON products FOR SELECT USING ((SELECT public.authorize('products.view')));
            """);
        // Apply the actual child schema and policies; the parent only needs identity/publication here.
        await ExecuteAsync(connection, File.ReadAllText(Path.Combine(Root(), "supabase/migrations/0022_create_product_variants.sql")));
        await ExecuteAsync(connection, File.ReadAllText(Path.Combine(Root(), "supabase/migrations/0029_product_specifications.sql")));
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(connection);
        await ExecuteAsync(connection, """
            DO $$ DECLARE published BOOLEAN; p UUID; image UUID; option_type UUID; value UUID; variant UUID;
            BEGIN
                FOREACH published IN ARRAY ARRAY[true, false] LOOP
                    p := gen_random_uuid(); image := gen_random_uuid(); option_type := gen_random_uuid();
                    value := gen_random_uuid(); variant := gen_random_uuid();
                    INSERT INTO products VALUES (p, published);
                    INSERT INTO product_images(id,product_id,object_key,position,is_primary) VALUES (image,p,'photo.png',0,true);
                    INSERT INTO product_option_types(id,product_id,name,position) VALUES (option_type,p,'Flavour',0);
                    INSERT INTO product_option_values(id,option_type_id,value,position) VALUES (value,option_type,'Peach',0);
                    INSERT INTO product_variants(id,product_id,sku,original_price,image_id,position) VALUES (variant,p,p::text,25,image,0);
                    INSERT INTO product_variant_option_values VALUES (variant,value);
                    INSERT INTO product_specifications(product_id,name,value,position) VALUES (p,'Capacity','2 ml',0);
                END LOOP;
            END $$;
            """);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();
    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync(); return connection;
    }
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TheShop.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("TheShop.slnx not found");
    }
    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "001_product-detail")]
    public async Task Read_GuestOrCustomer_SeesOnlyPublishedProductAndEveryChild(bool customer)
    {
        await using var connection = await OpenAsync();
        if (customer) await RbacTestSchema.SetJwtClaimsAsync(connection, _customer, _customerSession);
        await ExecuteAsync(connection, customer ? "SET ROLE authenticated" : "SET ROLE anon");
        foreach (var table in new[] { "products", "product_images", "product_option_types", "product_option_values",
                     "product_variants", "product_variant_option_values", "product_specifications" })
        {
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection);
            ((long)(await command.ExecuteScalarAsync())!).Should().Be(1, $"{table} hides unpublished rows for public shoppers");
        }
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Read_PrivilegedStaff_PublicationFilterStillExcludesUnpublishedParent()
    {
        await using var connection = await OpenAsync();
        await RbacTestSchema.SetJwtClaimsAsync(connection, _staff, _staffSession);
        await ExecuteAsync(connection, "SET ROLE authenticated");
        await using var all = new NpgsqlCommand("SELECT count(*) FROM products", connection);
        ((long)(await all.ExecuteScalarAsync())!).Should().Be(2, "staff RLS intentionally permits administrative reads");
        await using var published = new NpgsqlCommand("SELECT count(*) FROM products WHERE is_published = @published", connection);
        published.Parameters.AddWithValue("published", true);
        ((long)(await published.ExecuteScalarAsync())!).Should().Be(1);
    }
}
