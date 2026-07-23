using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Brand"/> entity: originally reference-data-only (Rehydrate, driving
/// the catalogue's Brand filter, FR-6 of product-catalogue), extended by add-brand into a full
/// aggregate with creation invariants (<see cref="Brand.Create"/>, RULE-1/RULE-3/RULE-5) and an
/// optional logo (<see cref="Brand.AttachLogo"/>, RULE-4).
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/add-brand/spec.md"/>
/// </summary>
public class BrandTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_WithValidData_ReturnsBrandWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var brand = Brand.Rehydrate(id, "Elf Bar", "elf-bar");

        brand.Id.Should().Be(id);
        brand.Name.Should().Be("Elf Bar");
        brand.Slug.Should().Be("elf-bar");
    }

    // =========================================================================
    // add-brand: Create — happy path (AC-1, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WithNameOnly_CreatesAnActiveBrandWithNoDescriptionOrLogo()
    {
        var brand = Brand.Create("Elf Bar", description: null, isActive: true);

        brand.Name.Should().Be("Elf Bar");
        brand.Description.Should().BeNull();
        brand.LogoPath.Should().BeNull();
        brand.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_CalledTwice_AssignsDifferentIds()
    {
        var first = Brand.Create("Elf Bar", null, true);
        var second = Brand.Create("Lost Mary", null, true);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_GeneratesALowercaseHyphenatedSlugFromTheName()
    {
        var brand = Brand.Create("Elf Bar 5000!", null, true);

        brand.Slug.Should().Be("elf-bar-5000");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromTheName()
    {
        var brand = Brand.Create("  Elf Bar  ", null, true);

        brand.Name.Should().Be("Elf Bar");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromTheDescription()
    {
        var brand = Brand.Create("Elf Bar", "  A vape brand.  ", true);

        brand.Description.Should().Be("A vape brand.");
    }

    // =========================================================================
    // add-brand: RULE-1 — name is required (AC-2)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "add-brand")]
    public void Create_WhenNameIsEmptyOrWhitespaceOnly_ThrowsBrandNameRequiredException(string name)
    {
        var act = () => Brand.Create(name, null, true);

        act.Should().Throw<BrandNameRequiredException>()
           .Which.MessageKey.Should().Be(BrandNameRequiredException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenNameIsNull_ThrowsBrandNameRequiredException()
    {
        var act = () => Brand.Create(null!, null, true);

        act.Should().Throw<BrandNameRequiredException>();
    }

    // =========================================================================
    // add-brand: RULE-3 — name/description length limits
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenNameExceeds100Characters_ThrowsBrandNameTooLongException()
    {
        var name = new string('a', 101);

        var act = () => Brand.Create(name, null, true);

        act.Should().Throw<BrandNameTooLongException>()
           .Which.MessageKey.Should().Be(BrandNameTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenNameIsExactly100Characters_Succeeds()
    {
        var name = new string('a', 100);

        var act = () => Brand.Create(name, null, true);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenDescriptionExceeds250Characters_ThrowsBrandDescriptionTooLongException()
    {
        var description = new string('a', 251);

        var act = () => Brand.Create("Elf Bar", description, true);

        act.Should().Throw<BrandDescriptionTooLongException>()
           .Which.MessageKey.Should().Be(BrandDescriptionTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenDescriptionIsExactly250Characters_Succeeds()
    {
        var description = new string('a', 250);

        var act = () => Brand.Create("Elf Bar", description, true);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WhenDescriptionIsWhitespaceOnly_IsStoredAsNull()
    {
        var brand = Brand.Create("Elf Bar", "    ", true);

        brand.Description.Should().BeNull();
    }

    // =========================================================================
    // add-brand: RULE-5 / FR-5 — status defaults + explicit Inactive (AC-1, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WithIsActiveTrue_CreatesAnActiveBrand()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WithIsActiveFalse_CreatesAnInactiveBrand()
    {
        var brand = Brand.Create("Elf Bar", null, false);

        brand.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // add-brand: AttachLogo — Behavior 2 / AC-4
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void AttachLogo_WithAStorageKey_SetsLogoPath()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.AttachLogo("brands/abc123/logo.webp");

        brand.LogoPath.Should().Be("brands/abc123/logo.webp");
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Create_WithoutAttachingALogo_LogoPathIsNull()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.LogoPath.Should().BeNull();
    }

    // =========================================================================
    // add-brand: Rehydrate — extended optional parameters (Infrastructure seam)
    // =========================================================================

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Rehydrate_WithDescriptionLogoPathAndIsActive_ReturnsBrandWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var brand = Brand.Rehydrate(id, "Elf Bar", "elf-bar", "A vape brand.", "brands/abc/logo.webp", false);

        brand.Id.Should().Be(id);
        brand.Description.Should().Be("A vape brand.");
        brand.LogoPath.Should().Be("brands/abc/logo.webp");
        brand.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Rehydrate_WithoutOptionalArguments_DefaultsToActiveWithNoDescriptionOrLogo()
    {
        var brand = Brand.Rehydrate(Guid.NewGuid(), "Elf Bar", "elf-bar");

        brand.Description.Should().BeNull();
        brand.LogoPath.Should().BeNull();
        brand.IsActive.Should().BeTrue();
    }
}

// =============================================================================
// AC → Test mapping (product-catalogue)
// =============================================================================
// No numbered AC maps directly to this reference-data entity; it supports FR-6
// (brand filter) and AC-6's underlying data.

// =============================================================================
// AC → Test mapping (add-brand)
// =============================================================================
// AC-1: Create_WithNameOnly_CreatesAnActiveBrandWithNoDescriptionOrLogo,
//        Create_WithIsActiveTrue_CreatesAnActiveBrand
// AC-2: Create_WhenNameIsEmptyOrWhitespaceOnly_ThrowsBrandNameRequiredException,
//        Create_WhenNameIsNull_ThrowsBrandNameRequiredException
// AC-4: AttachLogo_WithAStorageKey_SetsLogoPath
// AC-6: Create_WithoutAttachingALogo_LogoPathIsNull
// AC-7: Create_WithIsActiveFalse_CreatesAnInactiveBrand
// (RULE-3 length limits: Create_WhenNameExceeds100Characters_ThrowsBrandNameTooLongException,
//  Create_WhenNameIsExactly100Characters_Succeeds,
//  Create_WhenDescriptionExceeds250Characters_ThrowsBrandDescriptionTooLongException,
//  Create_WhenDescriptionIsExactly250Characters_Succeeds)
