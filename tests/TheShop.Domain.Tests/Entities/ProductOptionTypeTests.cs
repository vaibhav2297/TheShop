using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for <see cref="ProductOptionType"/>: name and value presence, and value uniqueness
/// within the type ignoring case and surrounding whitespace (RULE-9). Uniqueness of the type's
/// own name against a product's other option types is <see cref="Product.SetOptionTypes"/>'s
/// responsibility, covered in <c>ProductTests</c>.
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class ProductOptionTypeTests
{
    private static ProductOptionValueInput Value(string value) => new(null, value);

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithAValidNameAndValues_ReturnsTheTypeWithThoseValues()
    {
        var type = ProductOptionType.Create(null, "Flavour", 0, [Value("Mango"), Value("Mint")]);

        type.Name.Should().Be("Flavour");
        type.Values.Should().HaveCount(2);
        type.Values.Select(v => v.Value).Should().BeEquivalentTo(["Mango", "Mint"]);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_TrimsTheName()
    {
        var type = ProductOptionType.Create(null, "  Flavour  ", 0, [Value("Mango")]);

        type.Name.Should().Be("Flavour");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "create-product")]
    public void Create_WithABlankName_ThrowsOptionTypeNameRequiredException(string name)
    {
        var act = () => ProductOptionType.Create(null, name, 0, [Value("Mango")]);

        act.Should().Throw<OptionTypeNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithNoValues_ThrowsOptionTypeValueRequiredException()
    {
        var act = () => ProductOptionType.Create(null, "Flavour", 0, []);

        act.Should().Throw<OptionTypeValueRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithABlankValue_ThrowsOptionTypeValueRequiredException()
    {
        var act = () => ProductOptionType.Create(null, "Flavour", 0, [Value("   ")]);

        act.Should().Throw<OptionTypeValueRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithDuplicateValuesIgnoringCaseAndSpaces_ThrowsDuplicateOptionValueException()
    {
        var act = () => ProductOptionType.Create(null, "Flavour", 0, [Value("Mango"), Value("  MANGO  ")]);

        act.Should().Throw<DuplicateOptionValueException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithNoIdSupplied_AssignsANewId()
    {
        var first = ProductOptionType.Create(null, "Flavour", 0, [Value("Mango")]);
        var second = ProductOptionType.Create(null, "Flavour", 0, [Value("Mango")]);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithAnExistingId_ReusesIt()
    {
        var id = Guid.NewGuid();

        var type = ProductOptionType.Create(id, "Flavour", 0, [Value("Mango")]);

        type.Id.Should().Be(id, "an existing option type kept across an edit must keep its identity");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-9 (option type with a name and a list of values): Create_WithAValidNameAndValues_ReturnsTheTypeWithThoseValues
// AC-28 (duplicate/empty option names and values refused): Create_WithABlankName_ThrowsOptionTypeNameRequiredException,
//        Create_WithNoValues_ThrowsOptionTypeValueRequiredException, Create_WithABlankValue_ThrowsOptionTypeValueRequiredException,
//        Create_WithDuplicateValuesIgnoringCaseAndSpaces_ThrowsDuplicateOptionValueException
