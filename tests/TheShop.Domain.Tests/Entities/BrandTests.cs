using FluentAssertions;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Brand"/> reference-data entity, which drives the catalogue's
/// Brand filter (spec constraint: "The catalogue can be filtered by ... brand ...", FR-6).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class BrandTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithValidData_ReturnsBrandWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var brand = Brand.Create(id, "Elf Bar", "elf-bar");

        brand.Id.Should().Be(id);
        brand.Name.Should().Be("Elf Bar");
        brand.Slug.Should().Be("elf-bar");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// No numbered AC maps directly to this reference-data entity; it supports FR-6
// (brand filter) and AC-6's underlying data.
