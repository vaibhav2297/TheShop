using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Proves a French browser locale renders English storefront resources.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "remove-french")]
public sealed class LocalizationFallbackJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    /// <inheritdoc />
    protected override string? Locale => "fr-CA";

    [Fact]
    public async Task FrenchBrowserLocale_RendersEnglishCatalogueHeading()
    {
        await Page.GotoAsync("/products");

        await Page.GetByRole(AriaRole.Heading, new() { Name = Strings.Nav_Products }).WaitForAsync();
    }
}
