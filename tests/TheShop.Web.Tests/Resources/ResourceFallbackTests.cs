using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Verifies that a French UI culture falls back to the English resource set.
/// </summary>
public class ResourceFallbackTests
{
    [Theory]
    [InlineData(nameof(Strings.Nav_AdminPanel))]
    [InlineData(nameof(Strings.AddProduct_SaveButton))]
    [Trait("Feature", "remove-french")]
    public void ResourceLookup_UnderFrenchUiCulture_UsesEnglishText(string key)
    {
        var resourceManager = Strings.ResourceManager;
        var english = resourceManager.GetString(key, CultureInfo.InvariantCulture);
        var fallback = resourceManager.GetString(key, CultureInfo.GetCultureInfo("fr-CA"));

        english.Should().NotBeNullOrWhiteSpace();
        fallback.Should().Be(english);
    }
}
