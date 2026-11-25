using FluentAssertions;
using Microsoft.Playwright;
using TheShop.E2E.Tests.Fixtures;
using TheShop.E2E.Tests.Pages;
using TheShop.Web.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.E2E.Tests.Journeys;

/// <summary>Guest listing acceptance for .specs/002_variant-filters/spec.md, with unique local product assignments.</summary>
[Trait("Category", "E2E")]
[Trait("Feature", "002_variant-filters")]
public sealed class VariantFilterPublicJourneyTests(PlaywrightFixture playwright) : E2ETestBase(playwright)
{
    [Fact]
    public async Task AC4_MultipleChoicesMatchEitherValueAndRemainSelected()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data);
        await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
        await listing.SelectAsync(data.ColourKey, data.Colour, "Blue");
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("B blue"), data.Name("C red"), data.Name("D split"));
        await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
        await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToBeCheckedAsync();
    }

    [Fact]
    public async Task AC5_OneVariantMustSatisfyAllSelectedGroups()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data);
        await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
        await listing.SelectAsync(data.SizeKey, data.Size, "L");
        await listing.ExpectProductsAsync(data.Name("C red"));
        await Assertions.Expect(listing.Product(data.Name("D split"))).ToHaveCountAsync(0);
        await Assertions.Expect(listing.Product(data.Name("A mixed"))).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task AC6_MultipleMatchingVariantsDoNotDuplicateProductsAndClearRestoresPlainProduct()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data, (data.ColourKey, "red"));
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
        await Assertions.Expect(listing.Product(data.Name("C red"))).ToHaveCountAsync(1);
        await Assertions.Expect(listing.Product(data.Name("Plain"))).ToHaveCountAsync(0);
        await listing.ClearAsync();
        await Assertions.Expect(listing.Product(data.Name("Plain"))).ToBeVisibleAsync();
        Page.Url.Should().NotContain("variant");
    }

    [Fact]
    public async Task AC8_PublicBrandCategoryPriceAndSortCombineWithVariantRestriction()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data, ("price_min", "10"), ("price_max", "30"));
        await listing.SelectAsync(data.ColourKey, data.Colour, "Red");
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"));
        await Assertions.Expect(listing.Product(data.Name("A mixed")).Locator(".shop-product-card-price"))
            .ToHaveTextAsync(string.Format(Strings.ManageProducts_PriceFrom, CurrencyFormatter.Format(10)));
        Page.Url.Should().Contain(data.Brand.ToString()).And.Contain(data.Category.ToString()).And.Contain("price_min=10");
        await listing.OpenAsync(data, (data.ColourKey, "red"), ("sort", "price-desc"), ("price_min", "10"), ("price_max", "30"));
        await Assertions.Expect(listing.ResultNames).ToHaveTextAsync(new[] { data.Name("C red"), data.Name("A mixed") });
    }

    [Fact]
    public async Task AC11_RemovingOneChoiceAndFinalChoicePreservesOtherSelections()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data, (data.ColourKey, "red"), (data.ColourKey, "blue"), (data.SizeKey, "l"));
        await listing.SelectAsync(data.ColourKey, data.Colour, "Red", false);
        await listing.ExpectProductsAsync(data.Name("B blue"), data.Name("D split"));
        await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToBeCheckedAsync();
        await listing.SelectAsync(data.ColourKey, data.Colour, "Blue", false);
        await listing.ExpectProductsAsync(data.Name("B blue"), data.Name("C red"), data.Name("D split"));
        await listing.ExpandAsync(data.Size);
        await Assertions.Expect(listing.Choice(data.SizeKey, "L")).ToBeCheckedAsync();
        Uri.UnescapeDataString(Page.Url).Should().NotContain(data.ColourKey).And.Contain(data.SizeKey);
    }

    [Fact]
    public async Task AC12_NoMatchKeepsClearActionAndClearRestoresResults()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data, (data.ColourKey, "red"), (data.SizeKey, "xl"), ("price_max", "5"));
        await Assertions.Expect(listing.Root.GetByRole(AriaRole.Heading, new() { Name = Strings.Empty_NoResults, Exact = true })).ToBeVisibleAsync();
        await listing.ClearAsync();
        await Assertions.Expect(listing.Product(data.Name("Plain"))).ToBeVisibleAsync();
        Page.Url.Should().NotContain("variant").And.NotContain("price_max");
    }

    [Fact]
    public async Task AC16_OutdatedLinkExplainsRemovedChoiceAndPreservesValidCriteria()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await listing.OpenAsync(data, (data.ColourKey, "red"), (data.ColourKey, "removed"), ("price_min", "10"));
        await Assertions.Expect(listing.Snackbar).ToContainTextAsync(Strings.VariantFilters_SelectionsRemoved);
        await Page.WaitForURLAsync(url => !url.Contains("removed", StringComparison.Ordinal));
        Page.Url.Should().Contain(data.Brand.ToString()).And.Contain("price_min=10");
        await listing.ExpandAsync(data.Colour);
        await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
        await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).Not.ToBeCheckedAsync();
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
    }

    [Fact]
    public async Task AC18_DesktopAndMobileKeyboardControlsHaveNamesSelectionAndVisibleFocus()
    {
        await using var data = await VariantFilterData.CreateAsync(); var listing = new VariantListingPage(Page);
        await Page.SetViewportSizeAsync(1440, 900); await listing.OpenAsync(data);
        await listing.ExpandAsync(data.Colour);
        var trigger = listing.Sidebar.GetByRole(AriaRole.Button, new() { Name = data.Colour, Exact = true });
        await trigger.FocusAsync(); await Page.Keyboard.PressAsync("Tab"); await Page.Keyboard.PressAsync("Tab");
        var red = listing.Choice(data.ColourKey, "Red"); await listing.AssertFocusVisibleAsync(red);
        await Page.Keyboard.PressAsync("Space"); await Assertions.Expect(red).ToBeCheckedAsync();
        await listing.ExpectProductsAsync(data.Name("A mixed"), data.Name("C red"), data.Name("D split"));
        await Page.SetViewportSizeAsync(390, 844); await listing.OpenDrawerAsync();
        await listing.ExpandAsync(data.Colour, drawer: true);
        await Assertions.Expect(listing.Drawer.GetByRole(AriaRole.Heading, new() { Name = data.Colour, Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(listing.Choice(data.ColourKey, "Red", drawer: true)).ToBeCheckedAsync();
        await listing.SelectAsync(data.ColourKey, data.Colour, "Blue", drawer: true);
        await listing.CloseDrawerAsync(); await Page.SetViewportSizeAsync(1440, 900);
        await Assertions.Expect(listing.Choice(data.ColourKey, "Red")).ToBeCheckedAsync();
        await Assertions.Expect(listing.Choice(data.ColourKey, "Blue")).ToBeCheckedAsync();
        (await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= innerWidth + 1")).Should().BeTrue();
    }
}
