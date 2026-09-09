### Domain tests (xUnit + FluentAssertions)

```csharp
using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests;

/// <summary>
/// Tests for Cart entity business rules.
/// <see href=".specs/add-to-cart/spec.md"/>
/// </summary>
public class CartTests
{
    [Fact]
    [Trait("Feature", "add-to-cart")]
    public void AddItem_WithValidProductAndQuantity_AddsItemToCart()
    {
        // Arrange
        var cart = Cart.CreateFor(Guid.NewGuid());
        var product = ProductBuilder.WithStock(10);

        // Act
        cart.AddItem(product, quantity: 2);

        // Assert
        cart.Items.Should().HaveCount(1);
        cart.Items[0].Quantity.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Feature", "add-to-cart")]
    public void AddItem_WhenQuantityIsZeroOrNegative_ThrowsDomainException(int quantity)
    {
        var cart = Cart.CreateFor(Guid.NewGuid());
        var product = ProductBuilder.WithStock(10);

        var act = () => cart.AddItem(product, quantity);

        act.Should().Throw<DomainException>()
           .WithMessage("Quantity must be positive");
    }
}
```

- No mocks. Domain is pure C#.
- Use a `ProductBuilder` (or similar test data builder) if it exists; if not, create one in a `tests/TheShop.Domain.Tests/TestData/` folder.
- One assertion concept per test — use FluentAssertions chains for richness, not multiple unrelated asserts.
