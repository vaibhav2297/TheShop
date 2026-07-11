using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Web.Components.Common;
using Xunit;
using MudBlazor.Extensions;

namespace TheShop.Web.Tests.Components.Common;

/// <summary>
/// Tests for <see cref="ShopPagination"/> — the reusable pagination control behind the
/// catalogue's page navigation (FR-8, AC-8; spec constraint: "Applying or changing a filter
/// or sort returns the customer to the first page of results").
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class ShopPaginationTests : TestContext
{
    public ShopPaginationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_WithPageAndTotalPages_RendersMudPaginationWithThatMetadata()
    {
        var cut = Render<ShopPagination>(p => p
            .Add(c => c.Page, 2)
            .Add(c => c.TotalPages, 5));

        var pagination = cut.FindComponent<MudPagination>();
        pagination.Instance.GetState(x => x.Selected).Should().Be(2);
        pagination.Instance.GetState(x => x.Count).Should().Be(5);
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public async Task SelectPage_WhenUserChangesPage_InvokesPageChangedWithTheNewPage()
    {
        int? received = null;
        var cut = Render<ShopPagination>(p => p
            .Add(c => c.Page, 1)
            .Add(c => c.TotalPages, 5)
            .Add(c => c.PageChanged, (int page) => received = page));

        var pagination = cut.FindComponent<MudPagination>();
        await cut.InvokeAsync(() => pagination.Instance.SelectedChanged.InvokeAsync(3));

        received.Should().Be(3);
    }

    // =========================================================================
    // Boundary states
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_OnFirstPage_DoesNotThrow()
    {
        var act = () => Render<ShopPagination>(p => p
            .Add(c => c.Page, 1)
            .Add(c => c.TotalPages, 1));

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Render_OnLastPage_DoesNotThrow()
    {
        var act = () => Render<ShopPagination>(p => p
            .Add(c => c.Page, 5)
            .Add(c => c.TotalPages, 5));

        act.Should().NotThrow();
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-8: Render_WithPageAndTotalPages_RendersMudPaginationWithThatMetadata,
//        SelectPage_WhenUserChangesPage_InvokesPageChangedWithTheNewPage,
//        Render_OnFirstPage_DoesNotThrow, Render_OnLastPage_DoesNotThrow
