using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every manage-categories resource string — the add/edit forms' labels/buttons/
/// placeholders, the list's filter/sort/bulk-action strings, and every new error/outcome key —
/// resolves in both English and French (AC-25). Reads the compiled
/// <see cref="Strings.ResourceManager"/> directly against the invariant (English) and <c>fr</c>
/// cultures, mirroring the approach used for manage-brands' own localization completeness test.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CategoryLocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    private static readonly string[] ErrorAndConfirmationKeys =
    [
        "Category_NameRequired",
        "Category_NameTooLong",
        "Category_DescriptionTooLong",
        "Category_AlreadyExists",
        "Category_ImageInvalidType",
        "Category_ImageTooLarge",
        "Category_CreateFailed",
        "Category_Created",
        "Category_NotFound",
        "Category_UpdateFailed",
        "Category_DeleteFailed",
        "Category_StatusChangeFailed",
        "Category_PageInvalid",
        "Category_CategoryIdsRequired",
        "Category_InUse",
        "Category_BulkDeletePartial",
        "Category_BulkDeleteAllBlocked",
    ];

    private static readonly string[] AddCategoryFormKeys =
    [
        "AddCategory_PageTitle",
        "AddCategory_Heading",
        "AddCategory_NameLabel",
        "AddCategory_NamePlaceholder",
        "AddCategory_DescriptionLabel",
        "AddCategory_DescriptionPlaceholder",
        "AddCategory_ImageLabel",
        "AddCategory_ImageUploadButton",
        "AddCategory_ImageRemove",
        "AddCategory_StatusLabel",
        "AddCategory_StatusActive",
        "AddCategory_StatusInactive",
        "AddCategory_SaveButton",
        "AddCategory_CancelButton",
    ];

    private static readonly string[] ManageCategoriesListKeys =
    [
        "ManageCategories_PageTitle",
        "ManageCategories_Heading",
        "ManageCategories_SearchPlaceholder",
        "ManageCategories_ColumnName",
        "ManageCategories_ColumnDescription",
        "ManageCategories_ColumnStatus",
        "ManageCategories_ColumnActions",
        "ManageCategories_EmptyTitle",
        "ManageCategories_EmptyDescription",
        "ManageCategories_NoMatchTitle",
        "ManageCategories_NoMatchDescription",
        "ManageCategories_BulkSetActive",
        "ManageCategories_BulkSetInactive",
        "ManageCategories_BulkDelete",
        "ManageCategories_BulkDeactivate",
        "ManageCategories_InUseIndicator",
        "ManageCategories_DeletedSuccess",
        "ManageCategories_BulkDeletedSuccess",
        "ManageCategories_ActivatedSuccess",
        "ManageCategories_DeactivatedSuccess",
        "ManageCategories_DeleteConfirmTitle",
        "ManageCategories_DeleteConfirmBody",
        "ManageCategories_BulkDeleteConfirmTitle",
        "ManageCategories_BulkDeleteConfirmBody",
        "ManageCategories_DeactivateConfirmTitle",
        "ManageCategories_DeactivateConfirmBody",
        "ManageCategories_BulkDeactivateConfirmTitle",
        "ManageCategories_BulkDeactivateConfirmBody",
        "ManageCategories_EditAria",
        "ManageCategories_DeleteAria",
        "Filter_Status",
        "Filter_StatusActive",
        "Filter_StatusInactive",
    ];

    private static readonly string[] EditCategoryFormKeys =
    [
        "EditCategory_PageTitle",
        "EditCategory_Heading",
        "EditCategory_BackToList",
        "EditCategory_Success",
    ];

    private static readonly string[] SortKeys =
    [
        "Sort_NameAZ",
        "Sort_NameZA",
        "Sort_Newest",
        "Sort_Oldest",
    ];

    // =========================================================================
    // Validation + confirmation messages — English + French (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ErrorAndConfirmationNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ErrorAndConfirmationNameKeys() => ErrorAndConfirmationKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Add form labels/buttons/placeholders — English + French (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(AddCategoryFormNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void AddCategoryFormString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> AddCategoryFormNameKeys() => AddCategoryFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-categories list — English + French (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ManageCategoriesListNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void ManageCategoriesListString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ManageCategoriesListNameKeys() => ManageCategoriesListKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Edit form strings — English + French (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(EditCategoryFormNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void EditCategoryFormString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> EditCategoryFormNameKeys() => EditCategoryFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Sort labels, including the new Oldest order — English + French (AC-25, AC-32)
    // =========================================================================

    [Theory]
    [MemberData(nameof(SortNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void SortLabel_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> SortNameKeys() => SortKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AssertAvailableInBothCultures(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        var french = Strings.ResourceManager.GetString(key, French);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
        french.Should().NotBeNullOrWhiteSpace($"'{key}' must have a French resource string (AC-25)");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-25: ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglishAndFrench,
//         AddCategoryFormString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         ManageCategoriesListString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         EditCategoryFormString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         SortLabel_ForEveryKey_IsAvailableInEnglishAndFrench
// AC-32: SortLabel_ForEveryKey_IsAvailableInEnglishAndFrench (Sort_Oldest specifically)
