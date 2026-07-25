using System.Globalization;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Features.Admin;
using TheShop.Web.Common;
using TheShop.Web.Components.Admin;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Admin;

/// <summary>
/// Tests for <see cref="AdminModuleCard"/> — one dashboard tile: the module's localized label and
/// current count (or a neutral placeholder when unavailable), a keyboard-focusable link to the
/// module's management page, and an aria-label pairing the module name with its count (spec FR-3,
/// FR-4, FR-7; AC-2, AC-5; the a11y constraint "each count is announced to assistive technology
/// together with its module name").
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class AdminModuleCardTests : TestContext
{
    public AdminModuleCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    // =========================================================================
    // Label per module (FR-3, FR-8)
    // =========================================================================

    [Theory]
    [InlineData(AdminModule.Products, "AdminConsole_Module_Products")]
    [InlineData(AdminModule.Categories, "AdminConsole_Module_Categories")]
    [InlineData(AdminModule.Brands, "AdminConsole_Module_Brands")]
    [InlineData(AdminModule.Users, "AdminConsole_Module_Users")]
    [InlineData(AdminModule.Roles, "AdminConsole_Module_Roles")]
    [Trait("Feature", "admin-console")]
    public void Render_ForEachModule_ShowsTheCorrectLocalizedLabel(AdminModule module, string expectedKey)
    {
        var cut = Render<AdminModuleCard>(p => p.Add(c => c.Module, module));

        var expectedLabel = Strings.ResourceManager.GetString(expectedKey, CultureInfo.InvariantCulture)!;
        cut.Markup.Should().Contain(expectedLabel);
    }

    // =========================================================================
    // Navigation target per module (FR-4, AC-2)
    // =========================================================================

    [Theory]
    [InlineData(AdminModule.Products)]
    [InlineData(AdminModule.Categories)]
    [InlineData(AdminModule.Brands)]
    [InlineData(AdminModule.Users)]
    [InlineData(AdminModule.Roles)]
    [Trait("Feature", "admin-console")]
    public void Render_ForEachModule_LinksToItsManagementPage(AdminModule module)
    {
        var expectedHref = module switch
        {
            AdminModule.Products => Routes.Admin.ManageProducts,
            AdminModule.Categories => Routes.Admin.ManageCategories,
            AdminModule.Brands => Routes.Admin.ManageBrands,
            AdminModule.Users => Routes.Admin.ManageUsers,
            AdminModule.Roles => Routes.Admin.ManageRoles,
            _ => throw new ArgumentOutOfRangeException(nameof(module)),
        };

        var cut = Render<AdminModuleCard>(p => p.Add(c => c.Module, module));

        cut.Find($"a[href='{expectedHref}']").Should().NotBeNull();
    }

    // =========================================================================
    // Count / placeholder (FR-3, FR-7, AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithACount_ShowsTheCountAsText()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Products)
            .Add(c => c.Count, 42));

        cut.Markup.Should().Contain("42");
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithoutACount_ShowsThePlaceholderInsteadOfANumber()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Products)
            .Add(c => c.Count, (int?)null));

        cut.Markup.Should().Contain(Strings.AdminConsole_CountUnavailable);
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithZeroCount_ShowsZeroNotThePlaceholder()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Roles)
            .Add(c => c.Count, 0));

        cut.Markup.Should().Contain("0");
        cut.Markup.Should().NotContain(Strings.AdminConsole_CountUnavailable);
    }

    // =========================================================================
    // Accessibility — the count is announced together with its module name (a11y constraint)
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithACount_PairsTheCountWithAnAccessibleAriaLabelNamingTheModule()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Products)
            .Add(c => c.Count, 42));

        var expectedAria = string.Format(Strings.AdminConsole_CountAria, Strings.AdminConsole_Module_Products, "42");
        cut.Find($"[aria-label='{expectedAria}']").Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithoutACount_PairsThePlaceholderWithAnAccessibleAriaLabelNamingTheModule()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Roles)
            .Add(c => c.Count, (int?)null));

        var expectedAria = string.Format(
            Strings.AdminConsole_CountAria, Strings.AdminConsole_Module_Roles, Strings.AdminConsole_CountUnavailable);
        cut.Find($"[aria-label='{expectedAria}']").Should().NotBeNull();
    }

    // =========================================================================
    // Reusable component convention — forwards Class/Style to the root element
    // =========================================================================

    [Fact]
    [Trait("Feature", "admin-console")]
    public void Render_WithCustomClassAndStyle_ForwardsBothToTheRootElement()
    {
        var cut = Render<AdminModuleCard>(p => p
            .Add(c => c.Module, AdminModule.Products)
            .Add(c => c.Class, "my-custom-admin-card")
            .Add(c => c.Style, "border: 1px solid red;"));

        var root = cut.Find(".mud-paper");
        root.ClassList.Should().Contain("my-custom-admin-card");
        root.GetAttribute("style").Should().Contain("border: 1px solid red");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-2: Render_ForEachModule_LinksToItsManagementPage
// AC-5: Render_WithoutACount_ShowsThePlaceholderInsteadOfANumber,
//        Render_WithoutACount_PairsThePlaceholderWithAnAccessibleAriaLabelNamingTheModule
// AC-7 (supports label localization; full EN/FR coverage in AdminConsoleLocalizationTests):
//        Render_ForEachModule_ShowsTheCorrectLocalizedLabel
