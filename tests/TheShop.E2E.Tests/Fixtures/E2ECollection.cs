using Xunit;

namespace TheShop.E2E.Tests.Fixtures;

/// <summary>Test collection sharing one <see cref="AppHostFixture"/> and one <see cref="PlaywrightFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class E2ECollection
    : ICollectionFixture<AppHostFixture>, ICollectionFixture<PlaywrightFixture>
{
    /// <summary>The xUnit collection name shared by all E2E test classes.</summary>
    public const string Name = "E2E";
}
