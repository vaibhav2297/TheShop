using System.Globalization;
using FluentAssertions;
using TheShop.Domain.ValueObjects;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every RBAC-related resource string — role names, permission display names, and
/// access-denied messages — resolves in both English and French (AC-11). Reads the compiled
/// <see cref="Strings.ResourceManager"/> directly against the invariant (English) and
/// <c>fr</c> cultures, so this stays accurate even if new permissions are added to
/// <see cref="PermissionCatalogue"/> without a matching test update.
/// <see href=".specs/role-based-access-control/spec.md"/>
/// </summary>
public class RbacLocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    // Role display-name keys match roles.name_key post-migration-0009 (no "Role_" prefix).
    private static readonly string[] RoleKeys = ["Customer", "Support", "Admin", "SuperAdmin"];

    private static readonly string[] AccessDeniedKeys =
    [
        "Rbac_AccessDenied",
        "AccessDenied_Title",
        "AccessDenied_Message",
        "AccessDenied_BackToHome",
    ];

    private static readonly string[] AdminPanelKeys =
    [
        "Nav_AdminPanel",
        "ManageProducts_PageTitle",
        "ManageProducts_Heading",
        "ManageProducts_ShellNotice",
    ];

    // "admin_users.view" -> "AdminUsers", "products" -> "Products".
    private static string ToPascalCase(string snakeCase) =>
        string.Concat(snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries)
                                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    private static IEnumerable<object[]> AllPermissionDisplayNameKeys() =>
        PermissionCatalogue.All.Select(p =>
            (object[])[$"Permission_{ToPascalCase(p.Module)}_{ToPascalCase(p.Action)}"]);

    // =========================================================================
    // Role display names — English + French (FR-2, AC-11)
    // =========================================================================

    [Theory]
    [MemberData(nameof(RoleNameKeys))]
    [Trait("Feature", "role-based-access-control")]
    public void RoleDisplayName_ForEveryBuiltInRole_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> RoleNameKeys() => RoleKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Access-denied messages — English + French (AC-11)
    // =========================================================================

    [Theory]
    [MemberData(nameof(AccessDeniedNameKeys))]
    [Trait("Feature", "role-based-access-control")]
    public void AccessDeniedMessage_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> AccessDeniedNameKeys() => AccessDeniedKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Admin-panel navigation / shell strings — English + French (AC-11)
    // =========================================================================

    [Theory]
    [MemberData(nameof(AdminPanelNameKeys))]
    [Trait("Feature", "role-based-access-control")]
    public void AdminPanelString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> AdminPanelNameKeys() => AdminPanelKeys.Select(k => (object[])[k]);

    // =========================================================================
    // Permission display names — every catalogue permission, English + French (FR-3, AC-11)
    // =========================================================================

    [Theory]
    [MemberData(nameof(AllPermissionDisplayNameKeysData))]
    [Trait("Feature", "role-based-access-control")]
    public void PermissionDisplayName_ForEveryCataloguePermission_IsAvailableInEnglishAndFrench(string key)
    {
        AssertAvailableInBothCultures(key);
    }

    public static IEnumerable<object[]> AllPermissionDisplayNameKeysData() => AllPermissionDisplayNameKeys();

    // =========================================================================
    // Helpers
    // =========================================================================

    private static void AssertAvailableInBothCultures(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        var french = Strings.ResourceManager.GetString(key, French);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
        french.Should().NotBeNullOrWhiteSpace($"'{key}' must have a French resource string (AC-11)");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-11: RoleDisplayName_ForEveryBuiltInRole_IsAvailableInEnglishAndFrench,
//         AccessDeniedMessage_ForEveryKey_IsAvailableInEnglishAndFrench,
//         AdminPanelString_ForEveryKey_IsAvailableInEnglishAndFrench,
//         PermissionDisplayName_ForEveryCataloguePermission_IsAvailableInEnglishAndFrench
