using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every add-brand resource string — the form's labels/buttons/placeholders, its
/// validation and confirmation messages, and the manage-brands shell strings — resolves in both
/// English and French (FR-9, AC-9). Reads the compiled <see cref="Strings.ResourceManager"/>
/// directly against the invariant (English) and <c>fr</c> cultures, mirroring the approach used
/// for the RBAC feature's own localization completeness test.
/// <see href=".specs/add-brand/spec.md"/>
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
        "ManageBrands_ShellNotice",
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
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-9: ErrorOrConfirmationMessage_ForEveryKey_IsAvailableInEnglishAndFrench,
//        FormString_ForEveryKey_IsAvailableInEnglishAndFrench,
//        ManageBrandsShellString_ForEveryKey_IsAvailableInEnglishAndFrench
