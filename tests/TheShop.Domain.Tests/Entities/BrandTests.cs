using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Brand"/> entity: originally reference-data-only (Rehydrate, driving
/// the catalogue's Brand filter, FR-6 of product-catalogue), extended by add-brand into a full
/// aggregate with creation invariants (<see cref="Brand.Create"/>, RULE-1/RULE-3/RULE-5) and an
/// optional logo (<see cref="Brand.AttachLogo"/>, RULE-4), then extended again by manage-brands
/// with the mutation methods an edit exercises — <see cref="Brand.Rename"/> and
/// <see cref="Brand.ChangeDescription"/> (re-validating the same invariants as <c>Create</c>),
/// <see cref="Brand.Activate"/>/<see cref="Brand.Deactivate"/>, and <see cref="Brand.RemoveLogo"/>
/// (which hands back the discarded key for RULE-11 disposal).
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/add-brand/spec.md"/>
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class BrandTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_WithValidData_ReturnsBrandWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var brand = Brand.Rehydrate(id, "Elf Bar");

        brand.Id.Should().Be(id);
        brand.Name.Should().Be("Elf Bar");
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

        var brand = Brand.Rehydrate(id, "Elf Bar", "A vape brand.", "brands/abc/logo.webp", false);

        brand.Id.Should().Be(id);
        brand.Description.Should().Be("A vape brand.");
        brand.LogoPath.Should().Be("brands/abc/logo.webp");
        brand.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "add-brand")]
    public void Rehydrate_WithoutOptionalArguments_DefaultsToActiveWithNoDescriptionOrLogo()
    {
        var brand = Brand.Rehydrate(Guid.NewGuid(), "Elf Bar");

        brand.Description.Should().BeNull();
        brand.LogoPath.Should().BeNull();
        brand.IsActive.Should().BeTrue();
    }

    // =========================================================================
    // manage-brands: Rename — re-validates the same invariants as Create (RULE-1/RULE-3, AC-6/AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_WithAValidName_UpdatesTheName()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.Rename("Lost Mary");

        brand.Name.Should().Be("Lost Mary");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_TrimsLeadingAndTrailingWhitespaceFromTheName()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.Rename("  Lost Mary  ");

        brand.Name.Should().Be("Lost Mary");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-brands")]
    public void Rename_WhenNameIsEmptyOrWhitespaceOnly_ThrowsBrandNameRequiredException(string name)
    {
        var brand = Brand.Create("Elf Bar", null, true);

        var act = () => brand.Rename(name);

        act.Should().Throw<BrandNameRequiredException>()
           .Which.MessageKey.Should().Be(BrandNameRequiredException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_WhenNameIsNull_ThrowsBrandNameRequiredException()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        var act = () => brand.Rename(null!);

        act.Should().Throw<BrandNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_WhenNameIsEmptyOrWhitespaceOnly_LeavesTheOriginalNameUnchanged()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        try { brand.Rename(""); } catch (BrandNameRequiredException) { }

        brand.Name.Should().Be("Elf Bar", "a rejected save must leave the brand unchanged (RULE-1)");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_WhenNameExceeds100Characters_ThrowsBrandNameTooLongException()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        var name = new string('a', 101);

        var act = () => brand.Rename(name);

        act.Should().Throw<BrandNameTooLongException>()
           .Which.MessageKey.Should().Be(BrandNameTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_WhenNameIsExactly100Characters_Succeeds()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        var name = new string('a', 100);

        var act = () => brand.Rename(name);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Rename_ToItsOwnCurrentName_Succeeds()
    {
        // The Domain layer enforces no uniqueness at all (that is a repository concern, RULE-2) —
        // a no-op rename must not be treated any differently than any other valid name (AC-9).
        var brand = Brand.Create("Elf Bar", null, true);

        var act = () => brand.Rename("Elf Bar");

        act.Should().NotThrow();
        brand.Name.Should().Be("Elf Bar");
    }

    // =========================================================================
    // manage-brands: ChangeDescription — re-validates the same invariant as Create (RULE-3, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WithAValidDescription_UpdatesTheDescription()
    {
        var brand = Brand.Create("Elf Bar", "Original description.", true);

        brand.ChangeDescription("A new description.");

        brand.Description.Should().Be("A new description.");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_TrimsLeadingAndTrailingWhitespace()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        brand.ChangeDescription("  A vape brand.  ");

        brand.Description.Should().Be("A vape brand.");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WithNull_ClearsTheDescription()
    {
        var brand = Brand.Create("Elf Bar", "Original description.", true);

        brand.ChangeDescription(null);

        brand.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WithWhitespaceOnly_ClearsTheDescription()
    {
        var brand = Brand.Create("Elf Bar", "Original description.", true);

        brand.ChangeDescription("    ");

        brand.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WhenExceeds250Characters_ThrowsBrandDescriptionTooLongException()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        var description = new string('a', 251);

        var act = () => brand.ChangeDescription(description);

        act.Should().Throw<BrandDescriptionTooLongException>()
           .Which.MessageKey.Should().Be(BrandDescriptionTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WhenExceeds250Characters_LeavesTheOriginalDescriptionUnchanged()
    {
        var brand = Brand.Create("Elf Bar", "Original description.", true);

        try { brand.ChangeDescription(new string('a', 251)); } catch (BrandDescriptionTooLongException) { }

        brand.Description.Should().Be("Original description.");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void ChangeDescription_WhenExactly250Characters_Succeeds()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        var description = new string('a', 250);

        var act = () => brand.ChangeDescription(description);

        act.Should().NotThrow();
    }

    // =========================================================================
    // manage-brands: Activate / Deactivate — RULE-5, FR-11, FR-18, AC-12, AC-21
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Activate_OnAnInactiveBrand_SetsIsActiveToTrue()
    {
        var brand = Brand.Create("Elf Bar", null, isActive: false);

        brand.Activate();

        brand.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Activate_OnAnAlreadyActiveBrand_RemainsActive()
    {
        var brand = Brand.Create("Elf Bar", null, isActive: true);

        brand.Activate();

        brand.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Deactivate_OnAnActiveBrand_SetsIsActiveToFalse()
    {
        var brand = Brand.Create("Elf Bar", null, isActive: true);

        brand.Deactivate();

        brand.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void Deactivate_OnAnAlreadyInactiveBrand_RemainsInactive()
    {
        var brand = Brand.Create("Elf Bar", null, isActive: false);

        brand.Deactivate();

        brand.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // manage-brands: RemoveLogo — returns the previous key for disposal (RULE-11, AC-10)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void RemoveLogo_WhenALogoIsAttached_ClearsTheLogoPath()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");

        brand.RemoveLogo();

        brand.LogoPath.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void RemoveLogo_WhenALogoIsAttached_ReturnsThePreviousLogoPath()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");

        var previousPath = brand.RemoveLogo();

        previousPath.Should().Be("brands/abc/logo.webp");
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void RemoveLogo_WhenNoLogoIsAttached_ReturnsNullAndLogoPathStaysNull()
    {
        var brand = Brand.Create("Elf Bar", null, true);

        var previousPath = brand.RemoveLogo();

        previousPath.Should().BeNull();
        brand.LogoPath.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-brands")]
    public void AttachLogo_AfterRemoveLogo_ReplacesItWithTheNewKey()
    {
        var brand = Brand.Create("Elf Bar", null, true);
        brand.AttachLogo("brands/abc/logo.webp");
        brand.RemoveLogo();

        brand.AttachLogo("brands/abc/new-logo.webp");

        brand.LogoPath.Should().Be("brands/abc/new-logo.webp");
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

// =============================================================================
// AC → Test mapping (manage-brands)
// =============================================================================
// AC-6: Rename_WithAValidName_UpdatesTheName, ChangeDescription_WithAValidDescription_UpdatesTheDescription,
//        RemoveLogo_WhenALogoIsAttached_ClearsTheLogoPath, AttachLogo_AfterRemoveLogo_ReplacesItWithTheNewKey
// AC-7: Rename_WhenNameIsEmptyOrWhitespaceOnly_ThrowsBrandNameRequiredException,
//        Rename_WhenNameIsEmptyOrWhitespaceOnly_LeavesTheOriginalNameUnchanged
// AC-9: Rename_ToItsOwnCurrentName_Succeeds (Domain enforces no uniqueness at all — that is RULE-2,
//        a repository concern; this confirms the entity itself treats a no-op rename like any other)
// AC-10: RemoveLogo_WhenALogoIsAttached_ReturnsThePreviousLogoPath, RemoveLogo_WhenALogoIsAttached_ClearsTheLogoPath,
//         RemoveLogo_WhenNoLogoIsAttached_ReturnsNullAndLogoPathStaysNull, AttachLogo_AfterRemoveLogo_ReplacesItWithTheNewKey
// AC-12: Activate_OnAnInactiveBrand_SetsIsActiveToTrue, Deactivate_OnAnActiveBrand_SetsIsActiveToFalse
// AC-21: Activate_OnAnAlreadyActiveBrand_RemainsActive, Deactivate_OnAnAlreadyInactiveBrand_RemainsInactive
//         (SetBrandStatusHandler relies on this idempotence to exclude no-op brands from ChangedCount)
// (RULE-3 length limits on Rename/ChangeDescription: Rename_WhenNameExceeds100Characters_ThrowsBrandNameTooLongException,
//  Rename_WhenNameIsExactly100Characters_Succeeds, ChangeDescription_WhenExceeds250Characters_ThrowsBrandDescriptionTooLongException,
//  ChangeDescription_WhenExactly250Characters_Succeeds)
