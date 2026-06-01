using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Integration tests for the <c>customers</c> table schema and the
/// <c>customer_exists</c> SQL function used by
/// <see cref="TheShop.Infrastructure.Persistence.Repositories.SupabaseCustomerRepository"/>.
///
/// These tests spin up a real Postgres container (Testcontainers) and apply
/// exactly the migration specified in the plan (§10 Database Schema &amp; RLS Policies)
/// to verify the storage-level contracts the Application layer depends on.
///
/// Why raw SQL and not the Supabase SDK:
///   The Supabase SDK wraps PostgREST, which requires the full Supabase stack
///   (Auth, PostgREST process, etc.). A plain Postgres container cannot run
///   PostgREST, so we exercise the SQL contracts — unique index, not-null
///   constraints, <c>customer_exists</c> function — directly via Npgsql.
///   Mapper logic is covered separately in <see cref="CustomerMapperTests"/>.
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public sealed class SupabaseCustomerRepositoryTests : IAsyncLifetime
{
    // =========================================================================
    // Container + lifecycle
    // =========================================================================

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("shop_test")
        .WithUsername("shop")
        .WithPassword("shop")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _pg.StartAsync();
        await ApplySchemaAsync();
    }

    public async ValueTask DisposeAsync() => await _pg.DisposeAsync();

    // =========================================================================
    // Schema: not-null constraints (plan §10 — every column is NOT NULL)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenFirstNameIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var id = Guid.NewGuid();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
                VALUES ('{id}', NULL, 'Doe', '2000-01-01', 'nullfirst@example.com')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23502"); // not_null_violation
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenLastNameIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var id = Guid.NewGuid();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
                VALUES ('{id}', 'Jane', NULL, '2000-01-01', 'nulllast@example.com')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23502");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenEmailIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var id = Guid.NewGuid();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
                VALUES ('{id}', 'Jane', 'Doe', '2000-01-01', NULL)
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23502");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenDateOfBirthIsNull_ThrowsPostgresException()
    {
        await using var conn = await OpenAsync();
        var id = Guid.NewGuid();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""
                INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
                VALUES ('{id}', 'Jane', 'Doe', NULL, 'nulldob@example.com')
                """;
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23502");
    }

    // =========================================================================
    // Schema: UNIQUE index on lower(email) — AC-4 storage backstop
    // Plan §10: CREATE UNIQUE INDEX idx_customers_email ON customers (lower(email))
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenEmailAlreadyExistsCaseSensitive_ThrowsUniqueViolation()
    {
        // AC-4 backstop: "Each email address may have only one account" (spec constraint §4)
        await using var conn = await OpenAsync();

        await InsertCustomerAsync(conn, Guid.NewGuid(), "jane@example.com");

        var act = async () => await InsertCustomerAsync(conn, Guid.NewGuid(), "jane@example.com");

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23505"); // unique_violation
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenEmailAlreadyExistsDifferentCase_ThrowsUniqueViolation()
    {
        // Plan §10: index is on lower(email) — case-insensitive uniqueness
        await using var conn = await OpenAsync();

        await InsertCustomerAsync(conn, Guid.NewGuid(), "DupeCase@Example.com");

        var act = async () => await InsertCustomerAsync(conn, Guid.NewGuid(), "dupecase@example.com");

        await act.Should().ThrowAsync<PostgresException>()
            .Where(ex => ex.SqlState == "23505");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WhenEmailsAreDifferent_BothRowsInserted()
    {
        await using var conn = await OpenAsync();

        var act = async () =>
        {
            await InsertCustomerAsync(conn, Guid.NewGuid(), "alice@example.com");
            await InsertCustomerAsync(conn, Guid.NewGuid(), "bob@example.com");
        };

        await act.Should().NotThrowAsync();

        var count = await CountCustomersByEmailAsync(conn, "alice@example.com");
        count.Should().Be(1);
    }

    // =========================================================================
    // customer_exists function (plan §10 — used by ExistsForEmailAsync)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task CustomerExists_WhenEmailIsRegistered_ReturnsTrue()
    {
        // This is the SQL function that ICustomerRepository.ExistsForEmailAsync calls via RPC.
        await using var conn = await OpenAsync();
        await InsertCustomerAsync(conn, Guid.NewGuid(), "exists@example.com");

        var exists = await CallCustomerExistsAsync(conn, "exists@example.com");

        exists.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task CustomerExists_WhenEmailIsNotRegistered_ReturnsFalse()
    {
        await using var conn = await OpenAsync();

        var exists = await CallCustomerExistsAsync(conn, "nobody@example.com");

        exists.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task CustomerExists_WhenEmailCaseDiffers_ReturnsTrue()
    {
        // Plan §10: customer_exists uses lower(email) = lower(p_email) — case-insensitive
        await using var conn = await OpenAsync();
        await InsertCustomerAsync(conn, Guid.NewGuid(), "CaseCheck@Example.com");

        var exists = await CallCustomerExistsAsync(conn, "casecheck@example.com");

        exists.Should().BeTrue();
    }

    // =========================================================================
    // Happy-path round-trip: insert and select back
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task Insert_WithValidData_RowCanBeSelectedBack()
    {
        // Verifies the column shape expected by CustomerMapper.ToDomain matches the schema.
        await using var conn = await OpenAsync();
        var id = Guid.NewGuid();
        await InsertCustomerAsync(conn, id, "roundtrip@example.com",
            firstName: "Round", lastName: "Trip", dob: new DateOnly(1995, 3, 20));

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT id, first_name, last_name, email, date_of_birth FROM customers WHERE id = '{id}'";
        await using var reader = await cmd.ExecuteReaderAsync();

        var read = await reader.ReadAsync();
        read.Should().BeTrue("the row must exist after a successful insert");
        reader.GetGuid(0).Should().Be(id);
        reader.GetString(1).Should().Be("Round");
        reader.GetString(2).Should().Be("Trip");
        reader.GetString(3).Should().Be("roundtrip@example.com");
        DateOnly.FromDateTime(reader.GetDateTime(4)).Should().Be(new DateOnly(1995, 3, 20));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_pg.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    private static async Task InsertCustomerAsync(
        NpgsqlConnection conn,
        Guid id,
        string email,
        string firstName = "Jane",
        string lastName = "Doe",
        DateOnly? dob = null)
    {
        var dobValue = (dob ?? new DateOnly(2000, 1, 1)).ToString("yyyy-MM-dd");
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO customers (id, first_name, last_name, date_of_birth, email)
            VALUES ('{id}', '{firstName}', '{lastName}', '{dobValue}', '{email}')
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountCustomersByEmailAsync(NpgsqlConnection conn, string email)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM customers WHERE lower(email) = lower('{email}')";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    private static async Task<bool> CallCustomerExistsAsync(NpgsqlConnection conn, string email)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT customer_exists('{email}')";
        var result = await cmd.ExecuteScalarAsync();
        return (bool)result!;
    }

    // =========================================================================
    // Schema setup — mirrors migration 0001_create_customers.sql (plan §10)
    // No auth.users table exists in the plain Postgres container, so the FK is
    // omitted here; the constraint is tested via Supabase's own Auth layer in
    // the real environment. All other schema constraints are reproduced faithfully.
    // =========================================================================

    private async Task ApplySchemaAsync()
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS customers (
                id              UUID PRIMARY KEY,
                first_name      TEXT NOT NULL CHECK (length(trim(first_name)) > 0),
                last_name       TEXT NOT NULL CHECK (length(trim(last_name))  > 0),
                date_of_birth   DATE NOT NULL CHECK (date_of_birth < CURRENT_DATE),
                email           TEXT NOT NULL,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_customers_email
                ON customers (lower(email));

            CREATE OR REPLACE FUNCTION public.customer_exists(p_email TEXT)
            RETURNS BOOLEAN LANGUAGE sql STABLE AS $$
                SELECT EXISTS (SELECT 1 FROM customers WHERE lower(email) = lower(p_email))
            $$;
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Insert_WithValidData_RowCanBeSelectedBack
//        (customer row written after successful sign-up; mapper column mapping verified)
// AC-4: Insert_WhenEmailAlreadyExistsCaseSensitive_ThrowsUniqueViolation,
//        Insert_WhenEmailAlreadyExistsDifferentCase_ThrowsUniqueViolation
//        (storage-level backstop for spec constraint "each email at most one account")
// AC-5: CustomerExists_WhenEmailIsNotRegistered_ReturnsFalse
//        (sign-in guard reads this RPC to find the "no account" case)
// AC-9: n/a at schema level — Supabase invalidates previous OTP automatically on resend
