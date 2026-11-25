using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Auth;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.E2E.Tests.Pages.Admin;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Authorized cross-surface browser acceptance for .specs/002_variant-filters/spec.md, backed by local Supabase.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "002_variant-filters")]
public sealed class VariantFilterStaffJourneyTests(PlaywrightFixture playwright)
    : AuthenticatedE2ETestBase(playwright, AuthStateFactory.AdminEmail)
{
    [Fact]
    public async Task AC1_BothListingsShowConfiguredGroupsWithoutDuplicateChoices()
    {
        await using var data = await VariantFilterData.CreateAsync();
        foreach (var admin in new[] { false, true })
        {
            var listing = new VariantListingPage(Page, admin); await listing.OpenAsync(data);
            await listing.ExpandAsync(data.Colour); await listing.ExpandAsync(data.Size);
            await Assertions.Expect(listing.Group(data.ColourKey)).ToHaveCountAsync(1);
            await Assertions.Expect(listing.Group(data.SizeKey)).ToHaveCountAsync(1);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToHaveCountAsync(1);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToHaveCountAsync(1);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Green")).ToHaveCountAsync(0);
        }
        var management = new VariantListingPage(Page, true);
        await management.SelectAsync(data.ColourKey, data.Colour, "Blue");
        await Page.SetViewportSizeAsync(390, 844); await management.OpenDrawerAsync();
        await management.ExpandAsync(data.Colour, drawer: true);
        await Assertions.Expect(management.Choice(data.ColourKey, "Blue", drawer: true)).ToBeCheckedAsync();
    }

    [Fact]
    public async Task AC2_LaterPageChoicesRemainAvailableAfterSearchAndFiltering()
    {
        await using var data = await VariantFilterData.CreateAsync(additional: 13);
        foreach (var admin in new[] { false, true })
        {
            var listing = new VariantListingPage(Page, admin); await listing.OpenAsync(data);
            await listing.ExpandAsync(data.Colour);
            await Assertions.Expect(listing.Product(data.Name("Z12"))).ToHaveCountAsync(0);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Later")).ToBeVisibleAsync();
            if (admin) await new ManageProductsPage(Page).SearchAsync(data.Name("C red"));
            await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
            await Assertions.Expect(listing.Choice(data.ColourKey, "Later")).ToBeVisibleAsync();
            if (admin) await listing.ExpectProductsAsync(data.Name("C red"));
        }
    }

    [Fact]
    public async Task AC3_SavedConfigurationAndFinalRemovalRefreshBothListings()
    {
        await using var data = await VariantFilterData.CreateAsync(onlyMixed: true);
        var type = "Texture " + data.Token; var key = "variant:" + type.ToLowerInvariant();
        var catalogue = new VariantListingPage(Page); await catalogue.OpenAsync(data);
        await Assertions.Expect(catalogue.Group(key)).ToHaveCountAsync(0);
        await data.AddProductAsync("New type", variants: [("Cotton", "L", true, 20m)], groupName: type);
        await catalogue.ReloadAsync(); await catalogue.ExpandAsync(type);
        await Assertions.Expect(catalogue.Choice(key, "Cotton")).ToBeVisibleAsync();
        var management = new VariantListingPage(Page, true); await management.OpenAsync(data);
        await management.ExpandAsync(type); await Assertions.Expect(management.Choice(key, "Cotton")).ToBeVisibleAsync();
        await data.SetAvailableAsync("New type", 0, false); await catalogue.OpenAsync(data);
        await Assertions.Expect(catalogue.Group(key)).ToHaveCountAsync(0);
        await management.OpenAsync(data); await management.ExpandAsync(type);
        await Assertions.Expect(management.Choice(key, "Cotton")).ToBeVisibleAsync();
        await data.RemoveProductAsync("New type");
        await management.ReloadAsync(); await Assertions.Expect(management.Group(key)).ToHaveCountAsync(0);
        await catalogue.OpenAsync(data); await Assertions.Expect(catalogue.Group(key)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC7_PublicUsesActiveVariantsWhileManagementIncludesInactiveAndExcludesDeleted()
    {
        await using var data = await VariantFilterData.CreateAsync(onlyMixed: true);
        var catalogue = new VariantListingPage(Page); await catalogue.OpenAsync(data);
        await catalogue.ExpandAsync(data.Colour);
        await Assertions.Expect(catalogue.Group(data.ColourKey).GetByRole(AriaRole.Checkbox)).ToHaveCountAsync(1);
        await Assertions.Expect(catalogue.Choice(data.ColourKey, "Red")).ToBeVisibleAsync();
        await Assertions.Expect(catalogue.Choice(data.ColourKey, "Blue")).ToHaveCountAsync(0);
        await catalogue.SelectAsync(data.ColourKey, data.Colour, "Red"); await catalogue.ExpectProductsAsync(data.Name("A mixed"));
        var management = new VariantListingPage(Page, true); await management.OpenAsync(data);
        await management.SelectAsync(data.ColourKey, data.Colour, "Blue"); await management.ExpectProductsAsync(data.Name("A mixed"));
        await Assertions.Expect(management.Choice(data.ColourKey, "Green")).ToHaveCountAsync(0);
        await Assertions.Expect(management.Choice(data.ColourKey, "Secret")).ToBeVisibleAsync();
        await management.SelectAsync(data.ColourKey, data.Colour, "Blue", false);
        await management.SelectAsync(data.ColourKey, data.Colour, "Secret"); await management.ExpectProductsAsync(data.Name("Hidden"));
    }

    [Fact]
    public async Task AC9_ManagementCriteriaPreserveFullPriceRangeAndVariantCount()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page, true);
        await listing.OpenAsync(data, ("search", data.Name("A mixed")), ("status", "active"), ("price_min", "40"), ("price_max", "40"));
        await listing.SelectAsync(data.ColourKey, data.Colour, "Blue"); await listing.ExpectProductsAsync(data.Name("A mixed"));
        var row = listing.Product(data.Name("A mixed"));
        await Assertions.Expect(row).ToContainTextAsync(CurrencyFormatter.Format(10));
        await Assertions.Expect(row).ToContainTextAsync(CurrencyFormatter.Format(40));
        await Assertions.Expect(row).ToContainTextAsync(string.Format(Strings.ManageProducts_VariantCountMany, 2));
        Page.Url.Should().Contain("status=active").And.Contain("price_min=40").And.Contain(data.Brand.ToString()).And.Contain(data.Category.ToString());
    }

    [Fact]
    public async Task AC10_ChangingVariantsResetsPageAndSelectionWhilePagingRetainsCriteria()
    {
        await using var data = await VariantFilterData.CreateAsync(additional: 13); var listing = new VariantListingPage(Page, true);
        await listing.OpenAsync(data, ("sort", "name-desc")); await listing.PageAsync(2);
        var first = listing.Root.Locator("tbody tr").First;
        await first.GetByRole(AriaRole.Checkbox).CheckAsync();
        await Assertions.Expect(first.GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
        await Assertions.Expect(listing.Root.Locator("[aria-current=page]")).ToHaveTextAsync("1");
        await Assertions.Expect(listing.Root.Locator("tbody input[type=checkbox]:checked")).ToHaveCountAsync(0);
        await listing.PageAsync(2); await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
        Uri.UnescapeDataString(Page.Url).Should().Contain(data.ColourKey + "=red").And.Contain("sort=name-desc");
    }

    [Fact]
    public async Task AC13_RefreshSharedLinkAndHistoryRestoreCriteriaOnBothListings()
    {
        await using var data = await VariantFilterData.CreateAsync();
        foreach (var admin in new[] { false, true })
        {
            var listing = new VariantListingPage(Page, admin);
            await listing.OpenAsync(data, ("price_min", "10"));
            await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
            var redLink = Page.Url;
            await listing.SelectAsync(data.ColourKey, data.Colour, "Blue");
            await listing.BackAsync(); await listing.ExpandAsync(data.Colour);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
            await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).Not.ToBeCheckedAsync();
            await listing.ForwardAsync(); await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToBeCheckedAsync();
            await listing.ReloadAsync(); await listing.ExpandAsync(data.Colour);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
            await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToBeCheckedAsync();
            await listing.OpenLinkAsync(redLink); await listing.ExpandAsync(data.Colour);
            await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).Not.ToBeCheckedAsync();
            await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
            Page.Url.Should().Contain("price_min=10").And.Contain(data.Brand.ToString());
        }
    }

    [Fact]
    public async Task AC14_NoEligibleVariantsLeavesFixedControlsUsableOnBothListings()
    {
        await using var data = await VariantFilterData.CreateAsync(variants: false);
        foreach (var admin in new[] { false, true })
        {
            var listing = new VariantListingPage(Page, admin); await listing.OpenAsync(data);
            await Assertions.Expect(listing.Group(data.ColourKey)).ToHaveCountAsync(0);
            await Assertions.Expect(listing.Group(data.SizeKey)).ToHaveCountAsync(0);
            await Assertions.Expect(listing.Sidebar.GetByRole(AriaRole.Button, new() { Name = Strings.Filter_Price, Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(listing.Product(data.Name("Plain"))).ToBeVisibleAsync();
        }
    }

    [Fact]
    public async Task AC17_MetadataAndResultFailuresShowRetryAndRecoverOnBothListings()
    {
        await using var data = await VariantFilterData.CreateAsync();
        foreach (var admin in new[] { false, true })
        {
            var listing = new VariantListingPage(Page, admin);
            foreach (var metadata in new[] { true, false })
            {
                var endpoint = metadata ? (admin ? "get_admin_product_filters" : "get_catalogue_filters")
                    : (admin ? "admin_products_page" : "catalogue_products_page");
                var pattern = "**/rest/v1/rpc/" + endpoint;
                await Page.RouteAsync(pattern, route => route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{\"message\":\"E2E temporary failure\",\"code\":\"503\"}" }));
                await listing.OpenAsync(data, (data.ColourKey, "red"));
                await Assertions.Expect(listing.Error).ToContainTextAsync(Strings.VariantFilters_LoadFailed);
                await Assertions.Expect(listing.Root.GetByRole(AriaRole.Heading, new() { Name = admin ? Strings.ManageProducts_NoMatchTitle : Strings.Empty_NoResults, Exact = true })).ToHaveCountAsync(0);
                await Page.UnrouteAsync(pattern); await listing.Retry.ClickAsync();
                await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
                await Assertions.Expect(listing.Error).ToHaveCountAsync(0);
            }
        }
    }

    [Fact]
    public async Task AC19_InactiveOnlyMatchIsExcludedPubliclyAndIncludedForStaff()
    {
        await using var data = await VariantFilterData.CreateAsync();
        var catalogue = new VariantListingPage(Page); await catalogue.OpenAsync(data, (data.ColourKey, "blue"));
        await catalogue.ExpectProductsAsync(data.Name("B blue"), data.Name("D split"));
        await Assertions.Expect(catalogue.Product(data.Name("A mixed"))).ToHaveCountAsync(0);
        var management = new VariantListingPage(Page, true); await management.OpenAsync(data, (data.ColourKey, "blue"));
        await management.ExpectProductsAsync(data.Name("A mixed"), data.Name("B blue"), data.Name("D split"));
    }
}
