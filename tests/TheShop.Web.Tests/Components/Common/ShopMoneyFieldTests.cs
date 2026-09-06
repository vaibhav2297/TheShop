using System.Globalization;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopMoneyField"/> — the editable monetary amount used by the product form
/// and the variants table. The field's reason to exist is that a bound <see cref="decimal"/> cannot
/// carry its own currency, so these tests assert the pieces the wrapper settles on its callers'
/// behalf: the adorned symbol, the side it sits on, and the culture driving the decimal separator
/// (create-product AC-1).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ShopMoneyFieldTests : TestContext
{
    private static readonly CultureInfo OriginalUiCulture = CultureInfo.CurrentUICulture;

    public ShopMoneyFieldTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    private static void UseCulture(string cultureName) =>
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

    private static void RestoreCulture() => CultureInfo.CurrentUICulture = OriginalUiCulture;

    // =========================================================================
    // Currency adornment — symbol and placement follow the active culture
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_UnderEnglishCulture_AdornsTheSymbolBeforeTheInput()
    {
        UseCulture("en-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.99m));

            cut.Markup.Should().Contain("$");
            cut.Find(".mud-input-adornment-start").Should().NotBeNull();
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_UnderFrenchCulture_AdornsTheSymbolAfterTheInput()
    {
        UseCulture("fr-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.99m));

            cut.Markup.Should().Contain("$");
            cut.Find(".mud-input-adornment-end").Should().NotBeNull();
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Culture drives the editor, not just read-only display
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_UnderFrenchCulture_RendersTheAmountWithACommaDecimalSeparator()
    {
        UseCulture("fr-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.99m));

            cut.Find("input").GetAttribute("value").Should().Be("24,99");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_UnderEnglishCulture_RendersTheAmountToTwoDecimalPlaces()
    {
        UseCulture("en-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.5m));

            cut.Find("input").GetAttribute("value").Should().Be("24.50");
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Negative amounts — the field mirrors Money's non-negative rule
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_Always_FloorsTheInputAtZeroSoMoneyCannotGoNegative()
    {
        UseCulture("en-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.99m));

            cut.Find("input").GetAttribute("min").Should().Be("0");
        }
        finally
        {
            RestoreCulture();
        }
    }

    // =========================================================================
    // Consumer overrides still reach the field (Rule 24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithConsumerClass_ForwardsItToTheUnderlyingField()
    {
        UseCulture("en-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p
                .Add(c => c.Value, 24.99m)
                .Add(c => c.Class, "my-spacing"));

            cut.Markup.Should().Contain("my-spacing");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WhenDisabled_DisablesTheInput()
    {
        UseCulture("en-CA");
        try
        {
            var cut = Render<ShopMoneyField>(p => p
                .Add(c => c.Value, 24.99m)
                .Add(c => c.Disabled, true));

            cut.Find("input").HasAttribute("disabled").Should().BeTrue();
        }
        finally
        {
            RestoreCulture();
        }
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1 (price entry): Render_UnderEnglishCulture_AdornsTheSymbolBeforeTheInput,
//        Render_UnderFrenchCulture_AdornsTheSymbolAfterTheInput,
//        Render_UnderFrenchCulture_RendersTheAmountWithACommaDecimalSeparator,
//        Render_UnderEnglishCulture_RendersTheAmountToTwoDecimalPlaces,
//        Render_Always_FloorsTheInputAtZeroSoMoneyCannotGoNegative
// Rule 24 (Class/Style forwarding): Render_WithConsumerClass_ForwardsItToTheUnderlyingField
