using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class FeaturedSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:17-alpine")
        .WithDatabase("featured").WithUsername("shop").WithPassword("shop").Build();

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
            CREATE SCHEMA storage;
            CREATE TABLE storage.buckets(id text PRIMARY KEY,name text NOT NULL,public boolean NOT NULL,
                file_size_limit bigint,allowed_mime_types text[]);
            CREATE TABLE storage.objects(id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                bucket_id text NOT NULL,name text NOT NULL UNIQUE);
            CREATE FUNCTION storage.foldername(name text) RETURNS text[] LANGUAGE sql IMMUTABLE
                AS $$ SELECT string_to_array(name,'/') $$;
            ALTER TABLE storage.objects ENABLE ROW LEVEL SECURITY;
            """);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(conn);
        await RbacTestSchema.GrantStorageRlsRolePrivilegesAsync(conn);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "supabase", "migrations")))
            directory = directory.Parent;
        var path = Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("supabase/migrations"),
            "supabase", "migrations", "0037_manage_featured.sql");
        await ExecuteAsync(conn, await File.ReadAllTextAsync(path));
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
    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();
}

public sealed class FeaturedSchemaTests(FeaturedSchemaFixture database) : IClassFixture<FeaturedSchemaFixture>, IAsyncLifetime
{
    private NpgsqlConnection _conn = default!;
    private NpgsqlTransaction _transaction = default!;
    private const string Key1 = "featured/00000000-0000-0000-0000-000000000000/00000000000000000000000000000001.png";
    private const string Key2 = "featured/00000000-0000-0000-0000-000000000000/00000000000000000000000000000002.webp";
    public async ValueTask InitializeAsync()
    {
        _conn = await database.OpenAsync();
        _transaction = await _conn.BeginTransactionAsync();
        await ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','{Key1}'),('featured-images','{Key2}')");
    }
    public async ValueTask DisposeAsync() { await _transaction.RollbackAsync(); await _conn.DisposeAsync(); }
    private Task ExecuteAsync(string sql) => FeaturedSchemaFixture.ExecuteAsync(_conn, sql);
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var command = new NpgsqlCommand(sql, _conn);
        return await command.ExecuteScalarAsync();
    }
    private async Task<Guid> Permissions(params string[] actions)
    {
        await ExecuteAsync("RESET ROLE");
        await RbacTestSchema.ClearJwtClaimsAsync(_conn);
        var user = await RbacTestSchema.InsertAuthUserAsync(_conn);
        var role = $"Featured{Guid.NewGuid():N}";
        await ExecuteAsync($"INSERT INTO roles(name_key) VALUES ('{role}')");
        foreach (var action in actions)
            await ExecuteAsync($"""
                INSERT INTO role_permissions(role_id,permission_id) SELECT r.id,p.id FROM roles r,permissions p
                    WHERE r.name_key='{role}' AND p.code='homepage.{action}';
                """);
        await RbacTestSchema.AssignRoleByNameAsync(_conn, user, role);
        var session = await RbacTestSchema.InsertSessionAsync(_conn, user, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await RbacTestSchema.SetJwtClaimsAsync(_conn, user, session);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
        return user;
    }
    private Task Seed() => ExecuteAsync($"""
        INSERT INTO featured_sections(title,tile1_title,tile1_image_key,tile2_title,tile2_image_key,is_visible)
            VALUES ('Saved','One','{Key1}','Two','{Key2}',false);
        """);
    private async Task Save(bool create, string title = "Summer", string first = "First", string second = "Second",
        string key2 = Key2, bool visible = true)
    {
        await using var command = new NpgsqlCommand("""
            SELECT save_featured_admin(@create,@title,'Label',@first,'Shop','/products',@key1,
                NULL,@second,NULL,NULL,@key2,@visible)
            """, _conn);
        command.Parameters.AddWithValue("create", create);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("first", first);
        command.Parameters.AddWithValue("second", second);
        command.Parameters.AddWithValue("key1", Key1);
        command.Parameters.AddWithValue("key2", key2);
        command.Parameters.AddWithValue("visible", visible);
        await command.ExecuteNonQueryAsync();
    }
    private async Task Reject(Func<Task> action, string state)
    {
        await ExecuteAsync("SAVEPOINT expected_failure");
        await action.Should().ThrowAsync<PostgresException>().Where(x => x.SqlState == state);
        await ExecuteAsync("ROLLBACK TO SAVEPOINT expected_failure");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Bucket_IsPrivateWithExactSizeAndThreeAllowedTypes()
    {
        (await ScalarAsync("SELECT public FROM storage.buckets WHERE id='featured-images'")).Should().Be(false);
        (await ScalarAsync("SELECT file_size_limit FROM storage.buckets WHERE id='featured-images'")).Should().Be(5_242_880L);
        ((string[])(await ScalarAsync("SELECT allowed_mime_types FROM storage.buckets WHERE id='featured-images'"))!)
            .Should().BeEquivalentTo("image/png", "image/jpeg", "image/webp");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Schema_RequiredContentPairsAndSingletonRejectInvalidRows()
    {
        await Seed();
        await Reject(() => Seed(), "23505");
        foreach (var sql in new[] { "UPDATE featured_sections SET id=2", "UPDATE featured_sections SET title=' '",
                     "UPDATE featured_sections SET tile1_title=''", "UPDATE featured_sections SET tile2_title=' '",
                     "UPDATE featured_sections SET tile1_button_label='Shop'", "UPDATE featured_sections SET tile2_button_link='/products'" })
            await Reject(() => ExecuteAsync(sql), "23514");
        (await ScalarAsync("SELECT title FROM featured_sections")).Should().Be("Saved");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Rpc_CreateAndEditRequireViewPlusMatchingAction()
    {
        await Permissions("create");
        await Reject(() => Save(true), "42501");
        await Reject(async () => { await ScalarAsync("SELECT count(*) FROM get_featured_admin()"); }, "42501");
        await Permissions("view", "create");
        await Save(true);
        await Reject(() => Save(false), "42501");
        await Permissions("edit");
        await Reject(() => Save(false), "42501");
        await Reject(async () => { await ScalarAsync("SELECT count(*) FROM get_featured_admin()"); }, "42501");
        await Permissions("view", "edit");
        await Save(false, "Edited", visible: false);
        await Reject(() => Save(true), "42501");
        (await ScalarAsync("SELECT title FROM get_featured_admin()")).Should().Be("Edited");
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(false);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task ViewOnly_CanReadAbsentAndHiddenButCannotRpcOrDirectlyMutate()
    {
        await Permissions("view");
        (await ScalarAsync("SELECT count(*) FROM get_featured_admin()")).Should().Be(0L);
        await Reject(() => Save(true), "42501");
        await ExecuteAsync("RESET ROLE");
        await Seed();
        await ExecuteAsync("SET LOCAL ROLE authenticated");
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(false);
        await Reject(() => Save(false), "42501");
        await ExecuteAsync("UPDATE featured_sections SET title='Bypass'");
        (await ScalarAsync("SELECT title FROM featured_sections")).Should().Be("Saved");
        await Reject(() => ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','{Key1.Replace("01.png", "03.png")}')"), "42501");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task AnonymousCannotReadOrWriteConfigurationOrPrivateObjects()
    {
        await Seed();
        await ExecuteAsync("SET LOCAL ROLE anon");
        await Reject(async () => { await ScalarAsync("SELECT count(*) FROM get_featured_admin()"); }, "42501");
        await Reject(async () => { await ScalarAsync("SELECT count(*) FROM featured_sections"); }, "42501");
        (await ScalarAsync("SELECT count(*) FROM storage.objects WHERE bucket_id='featured-images'")).Should().Be(0L);
        await Reject(() => Save(true), "42501");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task CompleteSave_RoundTripsPlainTextBothTilesAndVisibility()
    {
        await Permissions("view", "create", "edit");
        await Save(true, "Staff's <Summer>", "First's title", "Second title", visible: false);
        (await ScalarAsync("SELECT title FROM get_featured_admin()")).Should().Be("Staff's <Summer>");
        (await ScalarAsync("SELECT tile1_title FROM get_featured_admin()")).Should().Be("First's title");
        (await ScalarAsync("SELECT tile1_button_link FROM get_featured_admin()")).Should().Be("/products");
        (await ScalarAsync("SELECT tile2_title FROM get_featured_admin()")).Should().Be("Second title");
        (await ScalarAsync("SELECT tile2_button_link IS NULL FROM get_featured_admin()")).Should().Be(true);
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(false);
        await Save(false, visible: true);
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(true);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task MissingSecondImageRejectsWholeSaveAndPreservesSavedRow()
    {
        await Seed();
        await Permissions("view", "edit");
        await Reject(() => Save(false, "Draft", "Changed first", "Changed second", Key2.Replace("02.webp", "03.webp")), "22023");
        (await ScalarAsync("SELECT title FROM get_featured_admin()")).Should().Be("Saved");
        (await ScalarAsync("SELECT tile1_title FROM get_featured_admin()")).Should().Be("One");
        (await ScalarAsync("SELECT tile2_title FROM get_featured_admin()")).Should().Be("Two");
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(false);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task DuplicateAddAndMissingEditReturnConflictAndLastSuccessfulCompleteSaveWins()
    {
        await Permissions("view", "create", "edit");
        await Reject(() => Save(false), "P0002");
        await Save(true);
        await Reject(() => Save(true, "Duplicate"), "23505");
        await Save(false, "Alice", "Alice One", "Alice Two", visible: false);
        await Save(false, "Bob older draft", "Bob One", "Bob Two", visible: true);
        (await ScalarAsync("SELECT count(*) FROM get_featured_admin()")).Should().Be(1L);
        (await ScalarAsync("SELECT title FROM get_featured_admin()")).Should().Be("Bob older draft");
        (await ScalarAsync("SELECT tile1_title FROM get_featured_admin()")).Should().Be("Bob One");
        (await ScalarAsync("SELECT tile2_title FROM get_featured_admin()")).Should().Be("Bob Two");
        (await ScalarAsync("SELECT is_visible FROM get_featured_admin()")).Should().Be(true);
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task Storage_OnlyOwnerUploadsWithViewAndActionAndObjectsRemainImmutable()
    {
        var owner = await Permissions("view", "create");
        var key = $"featured/{owner}/00000000000000000000000000000003.jpg";
        await ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','{key}')");
        (await ScalarAsync($"SELECT count(*) FROM storage.objects WHERE name='{key}'")).Should().Be(1L);
        await Reject(() => ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','{Key1.Replace("01.png", "04.png")}')"), "42501");
        await Reject(() => ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','featured/{owner}/bad.gif')"), "42501");
        await ExecuteAsync($"UPDATE storage.objects SET name='changed' WHERE name='{key}'");
        await ExecuteAsync($"DELETE FROM storage.objects WHERE name='{key}'");
        (await ScalarAsync($"SELECT count(*) FROM storage.objects WHERE name='{key}'")).Should().Be(1L);
        await Reject(() => ExecuteAsync("DELETE FROM featured_sections"), "42501");
        var editOwner = await Permissions("view", "edit");
        await ExecuteAsync($"INSERT INTO storage.objects(bucket_id,name) VALUES ('featured-images','featured/{editOwner}/00000000000000000000000000000004.webp')");
    }

    [Fact]
    [Trait("Feature", "005_homepage-featured")]
    public async Task DirectTableWritesCannotBypassCombinedPermissions()
    {
        await Seed();
        foreach (var action in new[] { "create", "edit" })
        {
            await Permissions(action);
            (await ScalarAsync("SELECT count(*) FROM featured_sections")).Should().Be(0L);
            await ExecuteAsync("UPDATE featured_sections SET title='Bypass'");
            await ExecuteAsync("RESET ROLE");
            (await ScalarAsync("SELECT title FROM featured_sections")).Should().Be("Saved");
            await ExecuteAsync("DELETE FROM featured_sections");
            await ExecuteAsync("SET LOCAL ROLE authenticated");
            // The BEFORE trigger runs before the INSERT policy check and cannot read private artwork without View.
            await Reject(() => Seed(), "22023");
            await ExecuteAsync("RESET ROLE");
            (await ScalarAsync("SELECT count(*) FROM featured_sections")).Should().Be(0L);
            await Seed();
        }
    }
}
