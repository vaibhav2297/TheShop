using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every add-brand resource string — the form's labels/buttons/placeholders, its
/// validation and confirmation messages, and the manage-brands shell strings — resolves in both
/// English and French (FR-9, AC-9). Extended by manage-brands to cover its own list/filter/
/// bulk-action strings, edit-form strings, and new error/outcome keys (FR-17, AC-19). Reads the
/// compiled <see cref="Strings.ResourceManager"/> directly against the invariant (English) and
/// <c>fr</c> cultures, mirroring the approach used for the RBAC feature's own localization
/// completeness test.
/// <see href=".specs/add-brand/spec.md"/>
/// <see href=".specs/manage-brands/spec.md"/>
/// </summary>
public class BrandLocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

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
    // Validation + confirmation messages — English + French (AC-9)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ErrorAndConfirmationNameKeys))]
    [Trait("Feature", "add-brand")]
    public void ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ErrorAndConfirmationNameKeys() =>
        ErrorAndConfirmationKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Form labels/buttons/placeholders — English + French (AC-9, AC-10)
    // =========================================================================

    [Theory]
    [MemberData(nameof(FormNameKeys))]
    [Trait("Feature", "add-brand")]
    public void FormString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> FormNameKeys() => FormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Manage-brands shell strings (post-save return target) — English + French (AC-9)
    // =========================================================================

    [Theory]
    [MemberData(nameof(ManageBrandsShellNameKeys))]
    [Trait("Feature", "add-brand")]
    public void ManageBrandsShellString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ManageBrandsShellNameKeys() => ManageBrandsShellKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AssertAvailableInBothCultures(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        var french = Strings.ResourceManager.GetString(key, French);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
        french.Should().NotBeNullOrWhiteSpace($"'{key}' must have a French resource string (AC-9)");
    }

    // =========================================================================
    // manage-brands: list/filter/sort/bulk-action strings — English + French (FR-17, AC-19)
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
    public void ManageBrandsListString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ManageBrandsListNameKeys() => ManageBrandsListKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-brands: edit form strings — English + French (FR-17, AC-19)
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
    public void EditBrandFormString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> EditBrandFormNameKeys() => EditBrandFormKeys.Select(k => (object[])[k]);

    // =========================================================================
    // manage-brands: new error/outcome keys — English + French (FR-17, AC-19)
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
    public void ManageBrandsErrorMessage_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> ManageBrandsErrorNameKeys() => ManageBrandsErrorKeys.Select(k => (object[])[k]);
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-9: ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglishAndFrench,
//        FormString_ForEveryKey_IsAvailableInEnglishAndFrench,
//        ManageBrandsShellString_ForEveryKey_IsAvailableInEnglishAndFrench

// =============================================================================
// AC → Test mapping (manage-brands)
// =============================================================================
// AC-19: ManageBrandsListString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         EditBrandFormString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         ManageBrandsErrorMessage_ForEveryKey_IsAvailableInEnglishAndFrench
