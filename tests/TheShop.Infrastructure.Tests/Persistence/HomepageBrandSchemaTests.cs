using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public sealed class HomepageBrandSchemaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:17-alpine")
        .WithDatabase("homepage_brand").WithUsername("shop").WithPassword("shop").Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await using var connection = await OpenAsync();
        await RbacTestSchema.ApplyAuthStubAsync(connection);
        await RbacTestSchema.ApplyRbacCoreAsync(connection);
        await RbacTestSchema.CreateRlsTestRolesAsync(connection);
        await ExecuteAsync(connection, """
            CREATE TABLE public.brands(id uuid PRIMARY KEY, name text NOT NULL,
                logo_path text, is_active boolean NOT NULL);
            GRANT SELECT ON public.brands TO anon, authenticated;
            """);
        await RbacTestSchema.GrantRlsRolePrivilegesAsync(connection);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "supabase", "migrations")))
            directory = directory.Parent;
        var path = Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("supabase/migrations"),
            "supabase", "migrations", "0040_manage_homepage_brands.sql");
        await ExecuteAsync(connection, await File.ReadAllTextAsync(path));
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_pg.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    public static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();
}

public sealed class HomepageBrandSchemaTests(HomepageBrandSchemaFixture database)
    : IClassFixture<HomepageBrandSchemaFixture>, IAsyncLifetime
{
    private NpgsqlConnection _connection = default!;
    private NpgsqlTransaction _transaction = default!;

    public async ValueTask InitializeAsync()
    {
        _connection = await database.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _connection.DisposeAsync();
    }

    private Task ExecuteAsync(string sql) => HomepageBrandSchemaFixture.ExecuteAsync(_connection, sql);
    private async Task<object?> ScalarAsync(string sql)
    {
        await using var command = new NpgsqlCommand(sql, _connection);
        return await command.ExecuteScalarAsync();
    }

    private async Task<Guid[]> BrandsAsync()
    {
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        await ExecuteAsync($"""
            INSERT INTO public.brands(id,name,is_active) VALUES
            ('{ids[0]}','Pens',true),('{ids[1]}','Pods',true),('{ids[2]}','Kits',true);
            """);
        return ids;
    }

    private async Task PermissionsAsync(params string[] actions)
    {
        await ExecuteAsync("RESET ROLE");
        await RbacTestSchema.ClearJwtClaimsAsync(_connection);
        var user = await RbacTestSchema.InsertAuthUserAsync(_connection);
        var role = $"HomepageBrand{Guid.NewGuid():N}";
        await ExecuteAsync($"INSERT INTO roles(name_key) VALUES ('{role}')");
        foreach (var action in actions)
            await ExecuteAsync($"""
                INSERT INTO role_permissions(role_id,permission_id)
                    SELECT r.id,p.id FROM roles r,permissions p
                    WHERE r.name_key='{role}' AND p.code='homepage.{action}';
                """);
        await RbacTestSchema.AssignRoleByNameAsync(_connection, user, role);
        var session = await RbacTestSchema.InsertSessionAsync(_connection, user,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await RbacTestSchema.SetJwtClaimsAsync(_connection, user, session);
        await ExecuteAsync("SET LOCAL ROLE authenticated");
    }

    private async Task SaveAsync(bool create, Guid[] ids, string title = "Shop by Brand", bool visible = true)
    {
        await using var command = new NpgsqlCommand("""
            SELECT public.save_homepage_brand_admin(@create,@title,@label,@link,@ids,@visible)
            """, _connection);
        command.Parameters.AddWithValue("create", create);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("label", "View All Brands");
        command.Parameters.AddWithValue("link", "/brands");
        command.Parameters.AddWithValue("ids", ids);
        command.Parameters.AddWithValue("visible", visible);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RejectAsync(Func<Task> action, string sqlState)
    {
        await ExecuteAsync("SAVEPOINT expected_failure");
        await action.Should().ThrowAsync<PostgresException>().Where(exception => exception.SqlState == sqlState);
        await ExecuteAsync("ROLLBACK TO SAVEPOINT expected_failure");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Schema_SingletonRequiredFieldsAndSelectionRules_RejectInvalidRows()
    {
        var ids = await BrandsAsync();
        await ExecuteAsync($"""
            INSERT INTO public.homepage_brand_section(title,button_label,button_link,brand_ids,is_visible)
            VALUES ('Saved','View','/brands',ARRAY['{ids[0]}'::uuid,'{ids[1]}'::uuid,'{ids[2]}'::uuid],false)
            """);
        await RejectAsync(() => ExecuteAsync($"""
            INSERT INTO public.homepage_brand_section(title,button_label,button_link,brand_ids)
            VALUES ('Duplicate','View','/brands',ARRAY['{ids[0]}'::uuid,'{ids[1]}'::uuid,'{ids[2]}'::uuid])
            """), "23505");
        await RejectAsync(() => ExecuteAsync("UPDATE public.homepage_brand_section SET title=' '"), "23514");
        await RejectAsync(() => ExecuteAsync("UPDATE public.homepage_brand_section SET button_label=''"), "23514");
        await RejectAsync(() => ExecuteAsync("UPDATE public.homepage_brand_section SET button_link='javascript:bad'"), "23514");
        await RejectAsync(() => ExecuteAsync("UPDATE public.homepage_brand_section SET brand_ids=ARRAY[]::uuid[], is_visible=true"), "23514");
        await RejectAsync(() => ExecuteAsync($"""
            UPDATE public.homepage_brand_section
            SET brand_ids=ARRAY['{ids[0]}'::uuid,'{ids[0]}'::uuid,'{ids[2]}'::uuid]
            """), "P0001");
        await ExecuteAsync($"UPDATE public.brands SET is_active=false WHERE id='{ids[1]}'");
        await PermissionsAsync("view", "edit");
        await RejectAsync(() => SaveAsync(false, ids), "P0001");
        (await ScalarAsync("SELECT is_visible FROM public.homepage_brand_section")).Should().Be(false);
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task Rpc_CreateAndEditRequireViewPlusMatchingAction_AndRestoreHiddenOrder()
    {
        var ids = await BrandsAsync();
        await PermissionsAsync("create");
        await RejectAsync(() => SaveAsync(true, ids), "42501");
        await PermissionsAsync("view", "create");
        await SaveAsync(true, [ids[2], ids[0], ids[1]], visible: false);
        await RejectAsync(() => SaveAsync(false, ids), "42501");
        (await ScalarAsync("SELECT is_visible FROM public.get_homepage_brand_admin()")).Should().Be(false);
        ((Guid[])(await ScalarAsync("SELECT brand_ids FROM public.get_homepage_brand_admin()"))!)
            .Should().Equal(ids[2], ids[0], ids[1]);
        await PermissionsAsync("view", "edit");
        await SaveAsync(false, ids, title: "Changed", visible: true);
        (await ScalarAsync("SELECT title FROM public.get_homepage_brand_admin()")).Should().Be("Changed");
        await RejectAsync(() => SaveAsync(true, ids), "42501");
    }

    [Fact]
    [Trait("Feature", "008_homepage-brand")]
    public async Task AnonymousCannotReadSection_AndCreateConflictOrMissingEditReturnExpectedStates()
    {
        var ids = await BrandsAsync();
        await PermissionsAsync("view", "create", "edit");
        await RejectAsync(() => SaveAsync(false, ids), "P0002");
        await SaveAsync(true, ids, visible: false);
        await RejectAsync(() => SaveAsync(true, ids), "23505");
        await ExecuteAsync("RESET ROLE");
        await ExecuteAsync("SET LOCAL ROLE anon");
        await RejectAsync(async () => { await ScalarAsync("SELECT count(*) FROM public.homepage_brand_section"); }, "42501");
        await RejectAsync(async () => { await ScalarAsync("SELECT count(*) FROM public.get_homepage_brand_admin()"); }, "42501");
    }
}

