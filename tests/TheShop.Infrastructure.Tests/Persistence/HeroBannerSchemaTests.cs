using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class HeroBannerSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine").WithDatabase("hero_banner")
        .WithUsername("shop").WithPassword("shop").Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var conn = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(conn);
        await RbacTestSchema.ApplyRbacCoreAsync(conn);
        await RbacTestSchema.CreateRlsTestRolesAsync(conn);
        await ExecuteAsync(conn, """
            CREATE SCHEMA storage;
            CREATE TABLE storage.buckets (
                id text PRIMARY KEY, name text NOT NULL, public boolean NOT NULL,
                file_size_limit bigint, allowed_mime_types text[]);
            CREATE TABLE storage.objects (
                id uuid PRIMARY KEY DEFAULT gen_random_uuid(), bucket_id text NOT NULL,
                name text NOT NULL UNIQUE);
            CREATE FUNCTION storage.foldername(name text) RETURNS text[]
                LANGUAGE sql IMMUTABLE AS $$ SELECT string_to_array(name, '/') $$;
            ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;
            """);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
        await RbacTestSchema.GrantStorageRlsRolePrivilegesAsync(conn);
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0034_manage_hero_banner.sql")));
        await ExecuteAsync(conn, await File.ReadAllTextAsync(Migration("0035_hero_banner_action_permissions.sql")));
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

public sealed class HeroBannerSchemaTests(HeroBannerSchemaFixture database)
    : IClassFixture<HeroBannerSchemaFixture>, IAsyncLifetime
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

    private Task ExecuteAsync(string sql) => HeroBannerSchemaFixture.ExecuteAsync(_conn, sql);
    private Task<object?> ScalarAsync(string sql) => HeroBannerSchemaFixture.ScalarAsync(_conn, sql);

    private async Task<Guid> UserWithPermissionAsync(string action)
    {
        await RbacTestSchema.ClearJwtClaimsAsync(_conn);
        var user = await RbacTestSchema.InsertAuthUserAsync(_conn);
        var role = $"Homepage{action}{Guid.NewGuid():N}";
        await ExecuteAsync($"""
            INSERT INTO roles(name_key) VALUES ('{role}');
            INSERT INTO role_permissions(role_id, permission_id)
                SELECT r.id, p.id FROM roles r, permissions p
                WHERE r.name_key='{role}' AND p.code='homepage.{action.ToLowerInvariant()}';
            """);
        await RbacTestSchema.AssignRoleByNameAsync(_conn, user, role);
        var session = await RbacTestSchema.InsertSessionAsync(_conn, user,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await RbacTestSchema.SetJwtClaimsAsync(_conn, user, session);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
        return user;
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task Migrations_CreatePrivateFiveMegabyteBucketAndSingletonTable()
    {
        (await ScalarAsync("SELECT file_size_limit FROM storage.buckets WHERE id='hero-banner-images'"))
            .Should().Be(5_242_880L);
        (await ScalarAsync("SELECT public FROM storage.buckets WHERE id='hero-banner-images'"))
            .Should().Be(false);
        await ExecuteAsync("""
            INSERT INTO hero_banners(heading,desktop_image_key)
            VALUES ('One','hero/00000000-0000-0000-0000-000000000000/00000000000000000000000000000000.png');
            """);
        var action = () => ExecuteAsync("""
            INSERT INTO hero_banners(id,heading,desktop_image_key)
            VALUES (2,'Two','hero/00000000-0000-0000-0000-000000000000/00000000000000000000000000000000.png');
            """);
        await action.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "23514");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task CreateOnlyUser_CanSaveButCannotReadManagementOrEdit()
    {
        var owner = await UserWithPermissionAsync("create");
        var key = $"hero/{owner}/00000000000000000000000000000000.png";
        await ExecuteAsync("RESET ROLE");
        await ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('hero-banner-images','{key}')");
        await ExecuteAsync("SET LOCAL ROLE authenticated");
        await ExecuteAsync($"""
            SELECT save_hero_banner_admin(true,'Heading',NULL,NULL,NULL,'{key}',NULL,true);
            """);
        (await ScalarAsync("SELECT count(*) FROM hero_banners")).Should().Be(1L);
        await ExecuteAsync("SAVEPOINT read_denial");
        var read = () => ScalarAsync("SELECT count(*) FROM get_hero_banner_admin()");
        await read.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
        await ExecuteAsync("ROLLBACK TO SAVEPOINT read_denial");
        var edit = () => ExecuteAsync($"SELECT save_hero_banner_admin(false,'Changed',NULL,NULL,NULL,'{key}',NULL,true)");
        await edit.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task EditOnlyUser_CanUpdateWithoutViewOrCreate()
    {
        var owner = await UserWithPermissionAsync("edit");
        var key = $"hero/{owner}/00000000000000000000000000000000.png";
        await ExecuteAsync("RESET ROLE");
        await ExecuteAsync($"""
            INSERT INTO storage.objects(bucket_id,name) VALUES ('hero-banner-images','{key}');
            INSERT INTO hero_banners(heading,desktop_image_key) VALUES ('Old','{key}');
            """);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
        await ExecuteAsync($"SELECT save_hero_banner_admin(false,'Changed',NULL,NULL,NULL,'{key}',NULL,false)");
        (await ScalarAsync("SELECT heading FROM hero_banners")).Should().Be("Changed");
        var create = () => ExecuteAsync($"SELECT save_hero_banner_admin(true,'Other',NULL,NULL,NULL,'{key}',NULL,true)");
        await create.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }

    [Fact]
    [Trait("Feature", "003_manage-hero-banner")]
    public async Task ViewOnlyUser_CanReadButCannotSave()
    {
        await UserWithPermissionAsync("view");
        (await ScalarAsync("SELECT count(*) FROM get_hero_banner_admin()")).Should().Be(0L);
        var save = () => ExecuteAsync("""
            SELECT save_hero_banner_admin(true,'Heading',NULL,NULL,NULL,
                'hero/00000000-0000-0000-0000-000000000000/00000000000000000000000000000000.png',NULL,true)
            """);
        await save.Should().ThrowAsync<PostgresException>().Where(ex => ex.SqlState == "42501");
    }
}
