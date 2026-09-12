using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="ProductSpecification"/>: name and value presence after trimming
/// (RULE-3), and identity assignment/reuse. Uniqueness of the row's name against a product's
/// other rows is <see cref="Product.SetSpecifications"/>'s responsibility, covered in
/// <c>ProductTests</c>.
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ProductSpecificationTests
{
    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithAValidNameAndValue_ReturnsTheRowWithThosePositionAndValues()
    {
        var row = ProductSpecification.Create(null, "Material", "Stainless steel", 0);

        row.Name.Should().Be("Material");
        row.Value.Should().Be("Stainless steel");
        row.Position.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_TrimsTheNameAndValue()
    {
        var row = ProductSpecification.Create(null, "  Material  ", "  Stainless steel  ", 0);

        row.Name.Should().Be("Material");
        row.Value.Should().Be("Stainless steel");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-description")]
    public void Create_WithABlankName_ThrowsSpecificationNameRequiredException(string name)
    {
        var act = () => ProductSpecification.Create(null, name, "Stainless steel", 0);

        act.Should().Throw<SpecificationNameRequiredException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "product-description")]
    public void Create_WithABlankValue_ThrowsSpecificationValueRequiredException(string value)
    {
        var act = () => ProductSpecification.Create(null, "Material", value, 0);

        act.Should().Throw<SpecificationValueRequiredException>();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithNoIdSupplied_AssignsANewId()
    {
        var first = ProductSpecification.Create(null, "Material", "Stainless steel", 0);
        var second = ProductSpecification.Create(null, "Material", "Stainless steel", 0);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithAnExistingId_ReusesIt()
    {
        var id = Guid.NewGuid();

        var row = ProductSpecification.Create(id, "Material", "Stainless steel", 0);

        row.Id.Should().Be(id, "an existing row kept across an edit must keep its identity");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_KeepsUnitsWithinTheValue()
    {
        var row = ProductSpecification.Create(null, "Capacity", "750 ml", 0);

        row.Value.Should().Be("750 ml", "units belong within value, not a separate field (RULE-3)");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1/AC-2 (add/edit a specification row with name and value): Create_WithAValidNameAndValue_ReturnsTheRowWithThosePositionAndValues,
//        Create_KeepsUnitsWithinTheValue
// AC-7 (blank name or value identified): Create_WithABlankName_ThrowsSpecificationNameRequiredException,
//        Create_WithABlankValue_ThrowsSpecificationValueRequiredException
