using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that manage-categories resource keys resolve to English text.
/// <see href=".specs/manage-categories/spec.md"/>
/// </summary>
public class CategoryLocalizationTests
{

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
    // Validation + confirmation messages — English (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ErrorAndConfirmationNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ErrorAndConfirmationNameKeys() => ErrorAndConfirmationKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Add form labels/buttons/placeholders — English (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(AddCategoryFormNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void AddCategoryFormString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> AddCategoryFormNameKeys() => AddCategoryFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-categories list — English (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ManageCategoriesListNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void ManageCategoriesListString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ManageCategoriesListNameKeys() => ManageCategoriesListKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Edit form strings — English (AC-25)
    // =========================================================================

    [Theory]
    [MemberData(nameof(EditCategoryFormNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void EditCategoryFormString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> EditCategoryFormNameKeys() => EditCategoryFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Sort labels, including the new Oldest order — English (AC-25, AC-32)
    // =========================================================================

    [Theory]
    [MemberData(nameof(SortNameKeys))]
    [Trait("Feature", "manage-categories")]
    public void SortLabel_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> SortNameKeys() => SortKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AssertAvailableInEnglish(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-25: ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglish,
//         AddCategoryFormString_ForEveryKey_IsAvailableInEnglish,
//         ManageCategoriesListString_ForEveryKey_IsAvailableInEnglish,
//         EditCategoryFormString_ForEveryKey_IsAvailableInEnglish,
//         SortLabel_ForEveryKey_IsAvailableInEnglish
// AC-32: SortLabel_ForEveryKey_IsAvailableInEnglish (Sort_Oldest specifically)
