### Infrastructure tests (xUnit + Testcontainers.PostgreSql + FluentAssertions)

Use a base fixture so containers are reused across the test class:

```csharp
using FluentAssertions;
using Testcontainers.PostgreSql;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        // Run migrations / schema setup here.
    }

    public async Task DisposeAsync() => await Container.DisposeAsync();
}

public class SupabaseProductRepositoryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fx;
    public SupabaseProductRepositoryTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    [Trait("Feature", "add-to-cart")]
    public async Task GetByIdAsync_WhenProductExists_ReturnsProduct()
    {
        // Arrange: insert directly via SQL or seeding helper.
        // Act: call repository.
        // Assert: returned domain object matches inserted row.
    }
}
```

- Reuse fixtures with `IClassFixture<T>` to keep test runs fast. Don't spin up a new container per test.
- Reset state between tests inside the class (truncate tables in a `[Fact]` setup helper).
- Infrastructure tests verify the mapping between database records and Domain entities. They do **not** re-test business rules — those belong in Domain tests.
- **Source the schema and mapping from the plan** (its *Data Model*, *Database Schema & RLS*, and Infrastructure phase) — table name, columns, unique indexes, RLS policies, and the record ↔ entity field mapping. Never infer columns from a guess or by reading production code for logic. If the plan doesn't pin down a column or policy you need, flag it instead of inventing it.
