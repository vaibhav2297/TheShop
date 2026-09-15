using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Guards the rule that a resource string embedding money uses a plain <c>{0}</c> placeholder.
/// Call sites format the amount through <c>CurrencyFormatter</c> before interpolation.
/// </summary>
public class MoneyStringFormatTests
{
    /// <summary>
    /// Every resource string whose placeholder carries a monetary amount.
    /// </summary>
    public static TheoryData<string> MoneyBearingKeys() =>
    [
        nameof(Strings.AddProduct_PriceRangeNoteWithValue),
        nameof(Strings.ManageProducts_PriceFrom),
    ];

    [Theory]
    [MemberData(nameof(MoneyBearingKeys))]
    [Trait("Feature", "create-product")]
    public void MoneyBearingString_UsesAPlainPlaceholderNotACurrencySpecifier(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
        english.Should().Contain("{0}", $"'{key}' must take its already-formatted amount through a plain placeholder");
        english.Should().NotContain(
            ":C",
            $"'{key}' must not format currency itself because amount arrives pre-formatted from CurrencyFormatter");
    }
}
