using FluentAssertions;
using TheShop.Domain.Entities;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Category"/> reference-data entity, which drives the catalogue's
/// Category filter (spec constraint: "The catalogue can be filtered by category...", FR-6).
/// <see href=".specs/product-catalogue/spec.md"/>
/// </summary>
public class CategoryTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Create_WithValidData_ReturnsCategoryWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var category = Category.Create(id, "Disposables", "disposables");

        category.Id.Should().Be(id);
        category.Name.Should().Be("Disposables");
        category.Slug.Should().Be("disposables");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// No numbered AC maps directly to this reference-data entity; it supports FR-6
// (category filter) and AC-6's underlying data.
