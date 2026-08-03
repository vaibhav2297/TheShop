using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="CategoryMapper"/> — the mapping contract between
/// <see cref="CategoryRecord"/> (the Supabase row model) and the <see cref="Category"/> domain
/// aggregate, sourced from the plan's Data Model (§4) and Database Schema (§10): the new
/// <c>description</c>, <c>image_path</c>, and <c>is_active</c> columns.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CategoryMapperTests
{
    private static readonly Guid Id = Guid.NewGuid();

    // =========================================================================
    // ToDomain — CategoryRecord → Category
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties()
    {
        var record = BuildRecord(description: "Single-use vape devices.", imagePath: "categories/abc/image.webp", isActive: false);

        var category = record.ToDomain();

        category.Id.Should().Be(Id);
        category.Name.Should().Be("Disposables");
        category.Description.Should().Be("Single-use vape devices.");
        category.ImagePath.Should().Be("categories/abc/image.webp");
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToDomain_WhenDescriptionAndImagePathAreNull_MapsBothAsNull()
    {
        var record = BuildRecord(description: null, imagePath: null, isActive: true);

        var category = record.ToDomain();

        category.Description.Should().BeNull();
        category.ImagePath.Should().BeNull();
    }

    // =========================================================================
    // ToRecord — Category → CategoryRecord
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToRecord_MapsAllDomainPropertiesToTheCorrespondingColumns()
    {
        var category = Category.Create("Disposables", "Single-use vape devices.", isActive: false);
        category.AttachImage("categories/abc/image.webp");

        var record = CategoryMapper.ToRecord(category);

        record.Id.Should().Be(category.Id);
        record.Name.Should().Be("Disposables");
        record.Description.Should().Be("Single-use vape devices.");
        record.ImagePath.Should().Be("categories/abc/image.webp");
        record.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ToRecord_SetsCreatedAtToTheCurrentUtcTime()
    {
        var before = DateTime.UtcNow;
        var category = Category.Create("Disposables", null, true);

        var record = CategoryMapper.ToRecord(category);

        var after = DateTime.UtcNow;
        record.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // =========================================================================
    // Round-trip
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void RoundTrip_ToRecord_ThenToDomain_PreservesEveryField()
    {
        var original = Category.Create("Disposables", "Single-use vape devices.", isActive: false);
        original.AttachImage("categories/abc/image.webp");

        var restored = CategoryMapper.ToRecord(original).ToDomain();

        restored.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Description.Should().Be(original.Description);
        restored.ImagePath.Should().Be(original.ImagePath);
        restored.IsActive.Should().Be(original.IsActive);
    }

    private static CategoryRecord BuildRecord(string? description, string? imagePath, bool isActive) => new()
    {
        Id = Id,
        Name = "Disposables",
        Description = description,
        ImagePath = imagePath,
        IsActive = isActive,
        CreatedAt = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
    };
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-6: RoundTrip_ToRecord_ThenToDomain_PreservesEveryField
//        (the mapper is the seam through which a created Category persists and reconstitutes)
// AC-8: ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties,
//        ToRecord_MapsAllDomainPropertiesToTheCorrespondingColumns (image_path column mapping)
// AC-7: ToDomain_WhenDescriptionAndImagePathAreNull_MapsBothAsNull,
//        ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties (is_active mapping)
