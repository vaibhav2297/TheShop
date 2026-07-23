using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;
using Xunit;

namespace TheShop.Infrastructure.Tests.Persistence;

/// <summary>
/// Unit tests for <see cref="BrandMapper"/> — the mapping contract between <see cref="BrandRecord"/>
/// (the Supabase row model) and the <see cref="Brand"/> domain aggregate, sourced from the plan's
/// Data Model (§4) and Database Schema (§10): the new <c>description</c>, <c>logo_path</c>, and
/// <c>is_active</c> columns.
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class BrandMapperTests
{
    private static readonly Guid Id = Guid.NewGuid();

    // =========================================================================
    // ToDomain — BrandRecord → Brand
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties()
    {
        var record = BuildRecord(description: "A vape brand.", logoPath: "brands/abc/logo.webp", isActive: false);

        var brand = record.ToDomain();

        brand.Id.Should().Be(Id);
        brand.Name.Should().Be("Elf Bar");
        brand.Slug.Should().Be("elf-bar");
        brand.Description.Should().Be("A vape brand.");
        brand.LogoPath.Should().Be("brands/abc/logo.webp");
        brand.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToDomain_WhenDescriptionAndLogoPathAreNull_MapsBothAsNull()
    {
        var record = BuildRecord(description: null, logoPath: null, isActive: true);

        var brand = record.ToDomain();

        brand.Description.Should().BeNull();
        brand.LogoPath.Should().BeNull();
    }

    // =========================================================================
    // ToRecord — Brand → BrandRecord
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToRecord_MapsAllDomainPropertiesToTheCorrespondingColumns()
    {
        var brand = Brand.Create("Elf Bar", "A vape brand.", isActive: false);
        brand.AttachLogo("brands/abc/logo.webp");

        var record = BrandMapper.ToRecord(brand);

        record.Id.Should().Be(brand.Id);
        record.Name.Should().Be("Elf Bar");
        record.Slug.Should().Be(brand.Slug);
        record.Description.Should().Be("A vape brand.");
        record.LogoPath.Should().Be("brands/abc/logo.webp");
        record.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void ToRecord_SetsCreatedAtToTheCurrentUtcTime()
    {
        var before = DateTime.UtcNow;
        var brand = Brand.Create("Elf Bar", null, true);

        var record = BrandMapper.ToRecord(brand);

        var after = DateTime.UtcNow;
        record.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // =========================================================================
    // Round-trip
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void RoundTrip_ToRecord_ThenToDomain_PreservesEveryField()
    {
        var original = Brand.Create("Elf Bar", "A vape brand.", isActive: false);
        original.AttachLogo("brands/abc/logo.webp");

        var restored = BrandMapper.ToRecord(original).ToDomain();

        restored.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.Slug.Should().Be(original.Slug);
        restored.Description.Should().Be(original.Description);
        restored.LogoPath.Should().Be(original.LogoPath);
        restored.IsActive.Should().Be(original.IsActive);
    }

    private static BrandRecord BuildRecord(string? description, string? logoPath, bool isActive) => new()
    {
        Id = Id,
        Name = "Elf Bar",
        Slug = "elf-bar",
        Description = description,
        LogoPath = logoPath,
        IsActive = isActive,
        CreatedAt = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
    };
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: RoundTrip_ToRecord_ThenToDomain_PreservesEveryField
//        (the mapper is the seam through which a created Brand persists and reconstitutes)
// AC-4: ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties,
//        ToRecord_MapsAllDomainPropertiesToTheCorrespondingColumns (logo_path column mapping)
// AC-6: ToDomain_WhenDescriptionAndLogoPathAreNull_MapsBothAsNull
// AC-7: ToDomain_MapsAllColumnsToTheCorrespondingDomainProperties (is_active mapping)
