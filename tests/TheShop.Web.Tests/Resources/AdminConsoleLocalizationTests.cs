using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every admin-console resource string — the page title/heading/subtitle, the five
/// module labels, the count-unavailable placeholder, the count aria template, the manage-action
/// button template, the empty-state message, and the account-menu entry label — resolves in both
/// English and French (FR-8, AC-7). Reads the compiled <see cref="Strings.ResourceManager"/>
/// directly against the invariant (English) and <c>fr</c> cultures, mirroring the approach used
/// for the add-brand and RBAC features' own localization completeness tests.
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class AdminConsoleLocalizationTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");

    private static readonly string[] DashboardKeys =
    [
        "Nav_AdminConsole",
        "AdminConsole_PageTitle",
        "AdminConsole_Heading",
        "AdminConsole_Subtitle",
        "AdminConsole_Module_Products",
        "AdminConsole_Module_Categories",
        "AdminConsole_Module_Brands",
        "AdminConsole_Module_Users",
        "AdminConsole_Module_Roles",
        "AdminConsole_CountUnavailable",
        "AdminConsole_CountAria",
        "AdminConsole_ManageAction",
        "AdminConsole_NoModules",
    ];

    [Theory]
    [MemberData(nameof(DashboardNameKeys))]
    [Trait("Feature", "admin-console")]
    public void DashboardString_ForEveryKey_IsAvailableInEnglishAndFrench(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);
        var french = Strings.ResourceManager.GetString(key, French);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
        french.Should().NotBeNullOrWhiteSpace($"'{key}' must have a French resource string (AC-7)");
    }

    public static IEnumerable<object[]> DashboardNameKeys() => DashboardKeys.Select(k => (object[])[k]);
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7: DashboardString_ForEveryKey_IsAvailableInEnglishAndFrench
