using FluentAssertions;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using Xunit;

namespace TheShop.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Category"/> entity: originally reference-data-only (Rehydrate,
/// driving the catalogue's Category filter, FR-6 of product-catalogue), extended by
/// manage-categories into a full aggregate with creation invariants (<see cref="Category.Create"/>,
/// RULE-1/RULE-3), an optional image (<see cref="Category.AttachImage"/>), and the mutation
/// methods an edit exercises — <see cref="Category.Rename"/> and
/// <see cref="Category.ChangeDescription"/> (re-validating the same invariants as <c>Create</c>),
/// <see cref="Category.Activate"/>/<see cref="Category.Deactivate"/>, and
/// <see cref="Category.RemoveImage"/> (which hands back the discarded key for RULE-11 disposal).
/// <see href=".specs/product-catalogue/spec.md"/>
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CategoryTests
{
    [Fact]
    [Trait("Feature", "product-catalogue")]
    public void Rehydrate_WithValidData_ReturnsCategoryWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var category = Category.Rehydrate(id, "Disposables");

        category.Id.Should().Be(id);
        category.Name.Should().Be("Disposables");
    }

    // =========================================================================
    // manage-categories: Create — happy path (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WithNameOnly_CreatesAnActiveCategoryWithNoDescriptionOrImage()
    {
        var category = Category.Create("Disposables", description: null, isActive: true);

        category.Name.Should().Be("Disposables");
        category.Description.Should().BeNull();
        category.ImagePath.Should().BeNull();
        category.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_CalledTwice_AssignsDifferentIds()
    {
        var first = Category.Create("Disposables", null, true);
        var second = Category.Create("Pod Systems", null, true);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromTheName()
    {
        var category = Category.Create("  Disposables  ", null, true);

        category.Name.Should().Be("Disposables");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromTheDescription()
    {
        var category = Category.Create("Disposables", "  Single-use vape devices.  ", true);

        category.Description.Should().Be("Single-use vape devices.");
    }

    // =========================================================================
    // manage-categories: RULE-1 — name is required (AC-9)
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenNameIsEmptyOrWhitespaceOnly_ThrowsCategoryNameRequiredException(string name)
    {
        var act = () => Category.Create(name, null, true);

        act.Should().Throw<CategoryNameRequiredException>()
           .Which.MessageKey.Should().Be(CategoryNameRequiredException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenNameIsNull_ThrowsCategoryNameRequiredException()
    {
        var act = () => Category.Create(null!, null, true);

        act.Should().Throw<CategoryNameRequiredException>();
    }

    // =========================================================================
    // manage-categories: RULE-3 — name/description length limits (AC-12)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenNameExceeds100Characters_ThrowsCategoryNameTooLongException()
    {
        var name = new string('a', 101);

        var act = () => Category.Create(name, null, true);

        act.Should().Throw<CategoryNameTooLongException>()
           .Which.MessageKey.Should().Be(CategoryNameTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenNameIsExactly100Characters_Succeeds()
    {
        var name = new string('a', 100);

        var act = () => Category.Create(name, null, true);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenDescriptionExceeds250Characters_ThrowsCategoryDescriptionTooLongException()
    {
        var description = new string('a', 251);

        var act = () => Category.Create("Disposables", description, true);

        act.Should().Throw<CategoryDescriptionTooLongException>()
           .Which.MessageKey.Should().Be(CategoryDescriptionTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenDescriptionIsExactly250Characters_Succeeds()
    {
        var description = new string('a', 250);

        var act = () => Category.Create("Disposables", description, true);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WhenDescriptionIsWhitespaceOnly_IsStoredAsNull()
    {
        var category = Category.Create("Disposables", "    ", true);

        category.Description.Should().BeNull();
    }

    // =========================================================================
    // manage-categories: RULE-5 / FR-7 — status defaults + explicit Inactive (AC-6, AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WithIsActiveTrue_CreatesAnActiveCategory()
    {
        var category = Category.Create("Disposables", null, true);

        category.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WithIsActiveFalse_CreatesAnInactiveCategory()
    {
        var category = Category.Create("Disposables", null, false);

        category.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // manage-categories: AttachImage — Behavior 4 / AC-6
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void AttachImage_WithAStorageKey_SetsImagePath()
    {
        var category = Category.Create("Disposables", null, true);

        category.AttachImage("categories/abc123/image.webp");

        category.ImagePath.Should().Be("categories/abc123/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Create_WithoutAttachingAnImage_ImagePathIsNull()
    {
        var category = Category.Create("Disposables", null, true);

        category.ImagePath.Should().BeNull();
    }

    // =========================================================================
    // manage-categories: Rehydrate — extended optional parameters (Infrastructure seam)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rehydrate_WithDescriptionImagePathAndIsActive_ReturnsCategoryWithSuppliedValues()
    {
        var id = Guid.NewGuid();

        var category = Category.Rehydrate(
            id, "Disposables", "Single-use vape devices.", "categories/abc/image.webp", false);

        category.Id.Should().Be(id);
        category.Description.Should().Be("Single-use vape devices.");
        category.ImagePath.Should().Be("categories/abc/image.webp");
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rehydrate_WithoutOptionalArguments_DefaultsToActiveWithNoDescriptionOrImage()
    {
        var category = Category.Rehydrate(Guid.NewGuid(), "Disposables");

        category.Description.Should().BeNull();
        category.ImagePath.Should().BeNull();
        category.IsActive.Should().BeTrue();
    }

    // =========================================================================
    // manage-categories: Rename — re-validates the same invariants as Create (RULE-1/RULE-3, AC-8/AC-24)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_WithAValidName_UpdatesTheName()
    {
        var category = Category.Create("Disposables", null, true);

        category.Rename("Pod Systems");

        category.Name.Should().Be("Pod Systems");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_TrimsLeadingAndTrailingWhitespaceFromTheName()
    {
        var category = Category.Create("Disposables", null, true);

        category.Rename("  Pod Systems  ");

        category.Name.Should().Be("Pod Systems");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "manage-categories")]
    public void Rename_WhenNameIsEmptyOrWhitespaceOnly_ThrowsCategoryNameRequiredException(string name)
    {
        var category = Category.Create("Disposables", null, true);

        var act = () => category.Rename(name);

        act.Should().Throw<CategoryNameRequiredException>()
           .Which.MessageKey.Should().Be(CategoryNameRequiredException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_WhenNameIsNull_ThrowsCategoryNameRequiredException()
    {
        var category = Category.Create("Disposables", null, true);

        var act = () => category.Rename(null!);

        act.Should().Throw<CategoryNameRequiredException>();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_WhenNameIsEmptyOrWhitespaceOnly_LeavesTheOriginalNameUnchanged()
    {
        var category = Category.Create("Disposables", null, true);

        try { category.Rename(""); } catch (CategoryNameRequiredException) { }

        category.Name.Should().Be("Disposables", "a rejected save must leave the category unchanged (RULE-1)");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_WhenNameExceeds100Characters_ThrowsCategoryNameTooLongException()
    {
        var category = Category.Create("Disposables", null, true);
        var name = new string('a', 101);

        var act = () => category.Rename(name);

        act.Should().Throw<CategoryNameTooLongException>()
           .Which.MessageKey.Should().Be(CategoryNameTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_WhenNameIsExactly100Characters_Succeeds()
    {
        var category = Category.Create("Disposables", null, true);
        var name = new string('a', 100);

        var act = () => category.Rename(name);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_ToItsOwnCurrentName_Succeeds()
    {
        // The Domain layer enforces no uniqueness at all (that is a repository concern, RULE-2) —
        // a no-op rename must not be treated any differently than any other valid name (AC-11).
        var category = Category.Create("Disposables", null, true);

        var act = () => category.Rename("Disposables");

        act.Should().NotThrow();
        category.Name.Should().Be("Disposables");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Rename_ToANameThatWouldHavePreviouslyClashedOnSlug_Succeeds()
    {
        // AC-24: with Slug retired (Decision 6), a rename like "Vape.Kits" no longer collides
        // with a "Vape Kits" category's slug — the Domain never derived one in the first place.
        var category = Category.Create("Vape Kits", null, true);

        var act = () => category.Rename("Vape.Kits");

        act.Should().NotThrow();
        category.Name.Should().Be("Vape.Kits");
    }

    // =========================================================================
    // manage-categories: ChangeDescription — re-validates the same invariant as Create (RULE-3, AC-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WithAValidDescription_UpdatesTheDescription()
    {
        var category = Category.Create("Disposables", "Original description.", true);

        category.ChangeDescription("A new description.");

        category.Description.Should().Be("A new description.");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_TrimsLeadingAndTrailingWhitespace()
    {
        var category = Category.Create("Disposables", null, true);

        category.ChangeDescription("  Single-use vape devices.  ");

        category.Description.Should().Be("Single-use vape devices.");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WithNull_ClearsTheDescription()
    {
        var category = Category.Create("Disposables", "Original description.", true);

        category.ChangeDescription(null);

        category.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WithWhitespaceOnly_ClearsTheDescription()
    {
        var category = Category.Create("Disposables", "Original description.", true);

        category.ChangeDescription("    ");

        category.Description.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WhenExceeds250Characters_ThrowsCategoryDescriptionTooLongException()
    {
        var category = Category.Create("Disposables", null, true);
        var description = new string('a', 251);

        var act = () => category.ChangeDescription(description);

        act.Should().Throw<CategoryDescriptionTooLongException>()
           .Which.MessageKey.Should().Be(CategoryDescriptionTooLongException.MessageResourceKey);
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WhenExceeds250Characters_LeavesTheOriginalDescriptionUnchanged()
    {
        var category = Category.Create("Disposables", "Original description.", true);

        try { category.ChangeDescription(new string('a', 251)); } catch (CategoryDescriptionTooLongException) { }

        category.Description.Should().Be("Original description.");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void ChangeDescription_WhenExactly250Characters_Succeeds()
    {
        var category = Category.Create("Disposables", null, true);
        var description = new string('a', 250);

        var act = () => category.ChangeDescription(description);

        act.Should().NotThrow();
    }

    // =========================================================================
    // manage-categories: Activate / Deactivate — RULE-5, FR-16/FR-18, AC-16
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Activate_OnAnInactiveCategory_SetsIsActiveToTrue()
    {
        var category = Category.Create("Disposables", null, isActive: false);

        category.Activate();

        category.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Activate_OnAnAlreadyActiveCategory_RemainsActive()
    {
        var category = Category.Create("Disposables", null, isActive: true);

        category.Activate();

        category.IsActive.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Deactivate_OnAnActiveCategory_SetsIsActiveToFalse()
    {
        var category = Category.Create("Disposables", null, isActive: true);

        category.Deactivate();

        category.IsActive.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void Deactivate_OnAnAlreadyInactiveCategory_RemainsInactive()
    {
        var category = Category.Create("Disposables", null, isActive: false);

        category.Deactivate();

        category.IsActive.Should().BeFalse();
    }

    // =========================================================================
    // manage-categories: RemoveImage — returns the previous key for disposal (RULE-11, AC-13)
    // =========================================================================

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void RemoveImage_WhenAnImageIsAttached_ClearsTheImagePath()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");

        category.RemoveImage();

        category.ImagePath.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void RemoveImage_WhenAnImageIsAttached_ReturnsThePreviousImagePath()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");

        var previousPath = category.RemoveImage();

        previousPath.Should().Be("categories/abc/image.webp");
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void RemoveImage_WhenNoImageIsAttached_ReturnsNullAndImagePathStaysNull()
    {
        var category = Category.Create("Disposables", null, true);

        var previousPath = category.RemoveImage();

        previousPath.Should().BeNull();
        category.ImagePath.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "manage-categories")]
    public void AttachImage_AfterRemoveImage_ReplacesItWithTheNewKey()
    {
        var category = Category.Create("Disposables", null, true);
        category.AttachImage("categories/abc/image.webp");
        category.RemoveImage();

        category.AttachImage("categories/abc/new-image.webp");

        category.ImagePath.Should().Be("categories/abc/new-image.webp");
    }
}

// =============================================================================
// AC → Test mapping (product-catalogue)
// =============================================================================
// No numbered AC maps directly to this reference-data entity; it supports FR-6
// (category filter) and AC-6's underlying data.

// =============================================================================
// AC → Test mapping (manage-categories)
// =============================================================================
// AC-6: Create_WithNameOnly_CreatesAnActiveCategoryWithNoDescriptionOrImage,
//        AttachImage_WithAStorageKey_SetsImagePath
// AC-7: Create_WithIsActiveFalse_CreatesAnInactiveCategory, Create_WithoutAttachingAnImage_ImagePathIsNull
// AC-8: Rename_WithAValidName_UpdatesTheName, ChangeDescription_WithAValidDescription_UpdatesTheDescription
// AC-9: Create_WhenNameIsEmptyOrWhitespaceOnly_ThrowsCategoryNameRequiredException,
//        Create_WhenNameIsNull_ThrowsCategoryNameRequiredException
// AC-11: Rename_ToItsOwnCurrentName_Succeeds (Domain enforces no uniqueness at all — that is RULE-2,
//         a repository concern; this confirms the entity itself treats a no-op rename like any other)
// AC-12: Create_WhenNameExceeds100Characters_ThrowsCategoryNameTooLongException,
//         Create_WhenNameIsExactly100Characters_Succeeds,
//         Create_WhenDescriptionExceeds250Characters_ThrowsCategoryDescriptionTooLongException,
//         Create_WhenDescriptionIsExactly250Characters_Succeeds
// AC-13: RemoveImage_WhenAnImageIsAttached_ReturnsThePreviousImagePath,
//         RemoveImage_WhenAnImageIsAttached_ClearsTheImagePath, AttachImage_AfterRemoveImage_ReplacesItWithTheNewKey
// AC-16: Activate_OnAnInactiveCategory_SetsIsActiveToTrue, Deactivate_OnAnActiveCategory_SetsIsActiveToFalse
//         (SetCategoryStatusHandler relies on the idempotent Activate_OnAnAlreadyActiveCategory_RemainsActive /
//          Deactivate_OnAnAlreadyInactiveCategory_RemainsInactive to exclude no-op categories from ChangedCount)
// AC-24: Rename_ToANameThatWouldHavePreviouslyClashedOnSlug_Succeeds (Slug retired, Decision 6)
