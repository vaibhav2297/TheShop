using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that add-brand and manage-brands resource keys resolve to English text.
/// <see href=".specs/add-brand/spec.md"/>
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class BrandLocalizationTests
{

    private static readonly string[] ErrorAndConfirmationKeys =
    [
        "Brand_NameRequired",
        "Brand_NameTooLong",
        "Brand_DescriptionTooLong",
        "Brand_AlreadyExists",
        "Brand_LogoInvalidType",
        "Brand_LogoTooLarge",
        "Brand_CreateFailed",
        "Brand_Created",
    ];

    private static readonly string[] FormKeys =
    [
        "AddBrand_PageTitle",
        "AddBrand_Heading",
        "AddBrand_NameLabel",
        "AddBrand_NamePlaceholder",
        "AddBrand_DescriptionLabel",
        "AddBrand_DescriptionPlaceholder",
        "AddBrand_LogoLabel",
        "AddBrand_LogoUploadButton",
        "AddBrand_LogoRemove",
        "AddBrand_StatusLabel",
        "AddBrand_StatusActive",
        "AddBrand_StatusInactive",
        "AddBrand_SaveButton",
        "AddBrand_CancelButton",
    ];

    private static readonly string[] ManageBrandsShellKeys =
    [
        "ManageBrands_PageTitle",
        "ManageBrands_Heading",
    ];

    // =========================================================================
    // Validation + confirmation messages — English (AC-9)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ErrorAndConfirmationNameKeys))]
    [Trait("Feature", "add-brand")]
    public void ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ErrorAndConfirmationNameKeys() =>
        ErrorAndConfirmationKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Form labels/buttons/placeholders — English (AC-9, AC-10)
    // =========================================================================

    [Theory]
    [MemberData(nameof(FormNameKeys))]
    [Trait("Feature", "add-brand")]
    public void FormString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> FormNameKeys() => FormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Manage-brands shell strings (post-save return target) — English (AC-9)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ManageBrandsShellNameKeys))]
    [Trait("Feature", "add-brand")]
    public void ManageBrandsShellString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ManageBrandsShellNameKeys() => ManageBrandsShellKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AssertAvailableInEnglish(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
    }

    // =========================================================================
    // manage-brands: list/filter/sort/bulk-action strings — English (FR-17, AC-19)
    // =========================================================================

    private static readonly string[] ManageBrandsListKeys =
    [
        "ManageBrands_PageTitle",
        "ManageBrands_Heading",
        "ManageBrands_SearchPlaceholder",
        "ManageBrands_ColumnName",
        "ManageBrands_ColumnDescription",
        "ManageBrands_ColumnStatus",
        "ManageBrands_ColumnActions",
        "ManageBrands_EmptyTitle",
        "ManageBrands_EmptyDescription",
        "ManageBrands_NoMatchTitle",
        "ManageBrands_NoMatchDescription",
        "ManageBrands_BulkSetActive",
        "ManageBrands_BulkSetInactive",
        "ManageBrands_BulkDelete",
        "ManageBrands_InUseIndicator",
        "ManageBrands_DeletedSuccess",
        "ManageBrands_BulkDeletedSuccess",
        "ManageBrands_ActivatedSuccess",
        "ManageBrands_DeactivatedSuccess",
        "ManageBrands_DeleteConfirmTitle",
        "ManageBrands_DeleteConfirmBody",
        "ManageBrands_BulkDeleteConfirmTitle",
        "ManageBrands_BulkDeleteConfirmBody",
        "ManageBrands_DeactivateConfirmTitle",
        "ManageBrands_DeactivateConfirmBody",
        "ManageBrands_BulkDeactivateConfirmTitle",
        "ManageBrands_BulkDeactivateConfirmBody",
        "ManageBrands_EditAria",
        "ManageBrands_DeleteAria",
        "Filter_Status",
        "Filter_StatusActive",
        "Filter_StatusInactive",
    ];

    [Theory]
    [MemberData(nameof(ManageBrandsListNameKeys))]
    [Trait("Feature", "manage-brands")]
    public void ManageBrandsListString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ManageBrandsListNameKeys() => ManageBrandsListKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-brands: edit form strings — English (FR-17, AC-19)
    // =========================================================================

    private static readonly string[] EditBrandFormKeys =
    [
        "EditBrand_PageTitle",
        "EditBrand_Heading",
        "EditBrand_BackToList",
        "EditBrand_Success",
    ];

    [Theory]
    [MemberData(nameof(EditBrandFormNameKeys))]
    [Trait("Feature", "manage-brands")]
    public void EditBrandFormString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> EditBrandFormNameKeys() => EditBrandFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-brands: new error/outcome keys — English (FR-17, AC-19)
    // =========================================================================

    private static readonly string[] ManageBrandsErrorKeys =
    [
        "Brand_NotFound",
        "Brand_UpdateFailed",
        "Brand_DeleteFailed",
        "Brand_StatusChangeFailed",
        "Brand_InUse",
        "Brand_BulkDeletePartial",
        "Brand_BulkDeleteAllBlocked",
        "Brand_PageInvalid",
        "Brand_BrandIdsRequired",
    ];

    [Theory]
    [MemberData(nameof(ManageBrandsErrorNameKeys))]
    [Trait("Feature", "manage-brands")]
    public void ManageBrandsErrorMessage_ForEveryKey_IsAvailableInEnglish(string key)
    {
        AssertAvailableInEnglish(key);
    }

    public static IEnumerable<object[]> ManageBrandsErrorNameKeys() => ManageBrandsErrorKeys.Select(k => (object[])[k]);
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-9: ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglish,
//        FormString_ForEveryKey_IsAvailableInEnglish,
//        ManageBrandsShellString_ForEveryKey_IsAvailableInEnglish

// =============================================================================
// AC → Test mapping (manage-brands)
// =============================================================================
// AC-19: ManageBrandsListString_ForEveryKey_IsAvailableInEnglish,
//         EditBrandFormString_ForEveryKey_IsAvailableInEnglish,
//         ManageBrandsErrorMessage_ForEveryKey_IsAvailableInEnglish
