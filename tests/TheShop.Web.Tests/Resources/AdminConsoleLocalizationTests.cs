using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that every admin-console resource key resolves to English text.
/// <see href=".specs/admin-console/spec.md"/>
/// </summary>
public class AdminConsoleLocalizationTests
{

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
    public void DashboardString_ForEveryKey_IsAvailableInEnglish(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
    }

    public static IEnumerable<object[]> DashboardNameKeys() => DashboardKeys.Select(k => (object[])[k]);
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-7: DashboardString_ForEveryKey_IsAvailableInEnglish
