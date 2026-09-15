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
/// Tests for <see cref="ShopMoneyField"/> and its fixed English Canadian money formatting.
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

    [Theory]
    [InlineData("en-CA")]
    [InlineData("fr-CA")]
    [Trait("Feature", "remove-french")]
    public void Render_UnderAnyUiCulture_UsesEnglishCanadianAdornmentAndValue(string cultureName)
    {
        UseCulture(cultureName);
        try
        {
            var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.5m));

            cut.Markup.Should().Contain("$");
            cut.Find(".mud-input-adornment-start").Should().NotBeNull();
            cut.Find("input").GetAttribute("value").Should().Be("24.50");
        }
        finally
        {
            RestoreCulture();
        }
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_Always_FloorsTheInputAtZeroSoMoneyCannotGoNegative()
    {
        var cut = Render<ShopMoneyField>(p => p.Add(c => c.Value, 24.99m));

        cut.Find("input").GetAttribute("min").Should().Be("0");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WithConsumerClass_ForwardsItToTheUnderlyingField()
    {
        var cut = Render<ShopMoneyField>(p => p
            .Add(c => c.Value, 24.99m)
            .Add(c => c.Class, "my-spacing"));

        cut.Markup.Should().Contain("my-spacing");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Render_WhenDisabled_DisablesTheInput()
    {
        var cut = Render<ShopMoneyField>(p => p
            .Add(c => c.Value, 24.99m)
            .Add(c => c.Disabled, true));

        cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }
}
