using FluentAssertions;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>
/// Storefront product catalogue journeys (.specs/product-catalogue/spec.md §6). Anonymous —
/// browsing the catalogue requires no persona.
/// </summary>
[Trait("Category", "E2E")]
[Trait("Feature", "product-catalogue")]
[Trait("Suite", "Smoke")]
public sealed class CatalogueJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC1_Catalogue_renders_seeded_products()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task AC6_Applying_a_brand_filter_narrows_the_visible_products()
    {
        var catalogue = new CataloguePage(Page);
        await catalogue.GotoAsync();

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
        await catalogue.ProductName("Vaporesso XROS 3").WaitForAsync(new() { Timeout = 15_000 });

        await catalogue.ExpandFilterGroupAsync(Strings.Filter_Brand);
        await catalogue.ToggleFilterOptionAsync("Elf Bar");

        await catalogue.ProductName("Elf Bar BC5000").WaitForAsync(new() { Timeout = 15_000 });
        (await catalogue.ProductName("Vaporesso XROS 3").CountAsync()).Should().Be(0,
            "the Vaporesso product must be filtered out once only the Elf Bar brand is selected");
    }
}
