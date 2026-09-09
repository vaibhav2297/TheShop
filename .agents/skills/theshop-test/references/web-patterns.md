### Web tests (xUnit + bUnit + NSubstitute + FluentAssertions)

```csharp
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Cart.Commands;
using TheShop.Web.Pages.Products;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Products;

/// <summary>
/// Tests for ProductDetail page.
/// <see href=".specs/add-to-cart/spec.md"/>
/// </summary>
public class ProductDetailTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly CartState _cartState = new();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public ProductDetailTests()
    {
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_cartState);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddMudServices();
    }

    [Fact]
    [Trait("Feature", "add-to-cart")]
    public void Render_WhenProductLoaded_ShowsAddToCartButton()
    {
        // Arrange: stub the query handler.
        // Act: render the component.
        // Assert: button is in the DOM and shows the localized label.
    }

    [Fact]
    [Trait("Feature", "add-to-cart")]
    public async Task ClickAddToCart_WhenMediatorReturnsSuccess_UpdatesCartStateAndShowsToast()
    {
        _mediator.Send(Arg.Any<AddToCartCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new CartDto(/* ... */)));

        var cut = RenderComponent<ProductDetail>(p => p.Add(c => c.Slug, "test-slug"));
        await cut.Find("[data-testid='add-to-cart']").ClickAsync(new());

        _cartState.Cart.Should().NotBeNull();
        _snackbar.Received(1).Add(Arg.Any<string>(), Severity.Success);
    }
}
```

- Register every injected service in the bUnit `TestContext`. Pages will throw if anything is missing.
- Always include `Services.AddMudServices()` — MudBlazor components need them.
- Use `data-testid` attributes for selectors when possible — `cut.Find("[data-testid='add-to-cart']")`. This is the same hook `$theshop-e2e` uses, so a testid added for one tier serves both. MudBlazor components take it through `UserAttributes="@(new Dictionary<string, object?> { ["data-testid"] = "…" })"`. If they don't exist in the page, write the test with a stable selector (e.g., button text via `Strings.AddToCart`) and note in your summary that adding `data-testid` attributes would improve test stability.
- For auth-guarded pages, inject a fake auth state and assert on navigation: `NavigationManager.Uri.Should().EndWith("/login");`

---
