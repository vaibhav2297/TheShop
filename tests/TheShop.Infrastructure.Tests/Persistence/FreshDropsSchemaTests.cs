using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class FreshDropsSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithDatabase("fresh_drops")
        .WithUsername("shop").WithPassword("shop").Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(conn);
        await RbacTestSchema.ApplyRbacCoreAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await ExecuteAsync(conn, """
            INSERT INTO permissions(code,module) VALUES
                ('homepage.view','homepage'),('homepage.create','homepage'),('homepage.edit','homepage');
            CREATE TABLE brands(id uuid PRIMARY KEY, name text NOT NULL);
            CREATE TABLE products(id uuid PRIMARY KEY, name text NOT NULL, brand_id uuid NOT NULL REFERENCES brands(id),
                display_price numeric NOT NULL, currency text NOT NULL, is_published boolean NOT NULL,
                created_at timestamptz NOT NULL);
            CREATE TABLE product_images(id uuid PRIMARY KEY, product_id uuid NOT NULL REFERENCES products(id),
                object_key text NOT NULL, is_primary boolean NOT NULL, position integer NOT NULL);
            """);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0036_manage_fresh_drops.sql")));
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    public static async Task ExecuteAsync(NpgsqlConnection conn, string sql)
    {
        await using var command = new NpgsqlCommand(sql, conn);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(NpgsqlConnection conn, string sql)
    {
        await using var command = new NpgsqlCommand(sql, conn);
        return await command.ExecuteScalarAsync();
    }

    private static string Migration(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "supabase", "migrations")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("supabase/migrations"),
            "supabase", "migrations", file);
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();
}

public sealed class FreshDropsSchemaTests(FreshDropsSchemaFixture database)
    : IClassFixture<FreshDropsSchemaFixture>, IAsyncLifetime
{
    private NpgsqlConnection _conn = default!;
    private NpgsqlTransaction _transaction = default!;

    public async ValueTask InitializeAsync()
    {
        _conn = await database.OpenAsync();
        _transaction = await _conn.BeginTransactionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _conn.DisposeAsync();
    }

    private Task ExecuteAsync(string sql) => FreshDropsSchemaFixture.ExecuteAsync(_conn, sql);
    private Task<object?> ScalarAsync(string sql) => FreshDropsSchemaFixture.ScalarAsync(_conn, sql);

    private async Task UsePermissionsAsync(params string[] actions)
    {
        await RbacTestSchema.ClearJwtClaimsAsync(_conn);
        var user = await RbacTestSchema.InsertAuthUserAsync(_conn);
        var role = $"FreshDrops{Guid.NewGuid():N}";
        await ExecuteAsync($"INSERT INTO roles(name_key) VALUES ('{role}')");
        foreach (var action in actions)
            await ExecuteAsync($"""
                INSERT INTO role_permissions(role_id,permission_id)
                    SELECT r.id,p.id FROM roles r,permissions p
                    WHERE r.name_key='{role}' AND p.code='homepage.{action}';
                """);
        await RbacTestSchema.AssignRoleByNameAsync(_conn, user, role);
        var session = await RbacTestSchema.InsertSessionAsync(_conn, user,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await RbacTestSchema.SetJwtClaimsAsync(_conn, user, session);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Schema_HiddenSingletonAndInvalidManualIds_RejectSecondOrInvalidRows()
    {
        await ExecuteAsync("INSERT INTO fresh_drops(title,selection_mode,is_visible) VALUES ('One','manual',false)");
        await ExecuteAsync("SAVEPOINT duplicate");
        var duplicate = () => ExecuteAsync("INSERT INTO fresh_drops(title,selection_mode) VALUES ('Two','newest')");
        await duplicate.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23505");
        await ExecuteAsync("ROLLBACK TO SAVEPOINT duplicate");
        await ExecuteAsync("SAVEPOINT invalid_selection");
        var invalid = () => ExecuteAsync("UPDATE fresh_drops SET manual_product_ids=ARRAY[gen_random_uuid(),gen_random_uuid(),gen_random_uuid(),gen_random_uuid()] WHERE id=1");
        await invalid.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
        await ExecuteAsync("ROLLBACK TO SAVEPOINT invalid_selection");
        (await ScalarAsync("SELECT is_visible FROM fresh_drops WHERE id=1")).Should().Be(false);
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task Rpc_CreateRequiresViewAndCreate_EditRequiresViewAndEdit()
    {
        await UsePermissionsAsync("create");
        await ExecuteAsync("SAVEPOINT denied_create");
        var denied = () => ExecuteAsync("SELECT save_fresh_drops_admin(true,'Title',NULL,NULL,'newest','{}',true)");
        await denied.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
        await ExecuteAsync("ROLLBACK TO SAVEPOINT denied_create");
        await ExecuteAsync("RESET ROLE");
        await UsePermissionsAsync("view", "create");
        await ExecuteAsync("SELECT save_fresh_drops_admin(true,'Title',NULL,NULL,'newest','{}',true)");
        (await ScalarAsync("SELECT count(*) FROM get_fresh_drops_admin()")).Should().Be(1L);
        await ExecuteAsync("SAVEPOINT denied_edit");
        var edit = () => ExecuteAsync("SELECT save_fresh_drops_admin(false,'Changed',NULL,NULL,'newest','{}',false)");
        await edit.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
        await ExecuteAsync("ROLLBACK TO SAVEPOINT denied_edit");
        await ExecuteAsync("RESET ROLE");
        await UsePermissionsAsync("view", "edit");
        await ExecuteAsync("SELECT save_fresh_drops_admin(false,'Changed',NULL,NULL,'newest','{}',false)");
        (await ScalarAsync("SELECT title FROM get_fresh_drops_admin()")).Should().Be("Changed");
    }

    [Fact]
    [Trait("Feature", "004_fresh-drops")]
    public async Task ProductCards_OnlyPublishedNewestAndSearchableByBrand()
    {
        var brand = Guid.NewGuid();
        var old = Guid.NewGuid();
        var newest = Guid.NewGuid();
        var unpublished = Guid.NewGuid();
        await ExecuteAsync($"INSERT INTO brands VALUES ('{brand}','Example Brand')");
        await ExecuteAsync($"""
            INSERT INTO products VALUES
                ('{old}','Old','{brand}',10,'CAD',true,'2026-01-01'),
                ('{newest}','New','{brand}',20,'CAD',true,'2026-02-01'),
                ('{unpublished}','Hidden','{brand}',30,'CAD',false,'2026-03-01');
            """);
        await UsePermissionsAsync("view");
        var ids = (Guid[])(await ScalarAsync("SELECT array_agg(id) FROM fresh_drops_product_cards(NULL,NULL,3,0)"))!;
        ids.Should().Equal(newest, old);
        (await ScalarAsync("SELECT count(*) FROM fresh_drops_product_cards(NULL,'example brand',24,0)"))
            .Should().Be(2L);
        (await ScalarAsync($"SELECT count(*) FROM fresh_drops_product_cards(ARRAY['{unpublished}'::uuid],NULL,3,0)"))
            .Should().Be(0L);
    }
}
