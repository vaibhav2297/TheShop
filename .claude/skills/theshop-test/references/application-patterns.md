### Application tests (xUnit + NSubstitute + FluentAssertions)

```csharp
using FluentAssertions;
using MediatR;
using NSubstitute;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Cart.Commands;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Application.Tests.Features.Cart;

/// <summary>
/// Tests for AddToCartHandler.
/// <see href=".specs/add-to-cart/spec.md"/>
/// </summary>
public class AddToCartHandlerTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICartRepository _carts = Substitute.For<ICartRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();

    private AddToCartHandler CreateSut() => new(_products, _carts, _user, _mapper);

    [Fact]
    [Trait("Feature", "add-to-cart")]
    public async Task Handle_WithValidProductAndQuantity_ReturnsSuccessResult()
    {
        // Arrange
        var product = ProductBuilder.WithStock(10);
        var userId = Guid.NewGuid();
        _user.Id.Returns(userId);
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        _carts.GetForUserAsync(userId, Arg.Any<CancellationToken>()).Returns((Cart?)null);

        var sut = CreateSut();
        var cmd = new AddToCartCommand(product.Id, Quantity: 2);

        // Act
        var result = await sut.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _carts.Received(1).SaveAsync(Arg.Any<Cart>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "add-to-cart")]
    public async Task Handle_WhenProductNotFound_ReturnsFailureResult()
    {
        _products.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Product?)null);
        var sut = CreateSut();

        var result = await sut.Handle(new AddToCartCommand(Guid.NewGuid(), 1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("ProductNotFound"); // resource key, not translated message
    }
}
```

- Mock every constructor dependency. Use `Substitute.For<T>()` and `.Returns(...)` for setup, `.Received(N)` for verification.
- Use `Arg.Any<T>()` for cancellation tokens; assert specific values where they matter.
- For validator tests, use `FluentValidation.TestHelper`: `validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Quantity);`
