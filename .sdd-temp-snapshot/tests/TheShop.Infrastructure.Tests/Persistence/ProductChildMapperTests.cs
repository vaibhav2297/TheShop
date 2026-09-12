using FluentAssertions;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="ProductChildMapper"/>'s <see cref="ProductSpecificationRecord"/> →
/// <see cref="TheShop.Domain.Entities.ProductSpecification"/> mapping, backing the edit-form
/// reload (AC-1, AC-2).
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ProductChildMapperTests
{
    [Fact]
    [Trait("Feature", "product-description")]
    public void ToDomain_MapsIdNameValueAndPosition()
    {
        var id = Guid.NewGuid();
        var record = new ProductSpecificationRecord
        {
            Id = id,
            ProductId = Guid.NewGuid(),
            Name = "Material",
            Value = "Stainless steel",
            Position = 2,
        };

        var specification = record.ToDomain();

        specification.Id.Should().Be(id);
        specification.Name.Should().Be("Material");
        specification.Value.Should().Be("Stainless steel");
        specification.Position.Should().Be(2);
    }
}
