using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Guards the one rule that keeps prices culture-correct in composed sentences: a resource string
/// that embeds a money value must take it through a plain <c>{0}</c> placeholder, never a
/// culture-sensitive <c>{0:C}</c>. <c>string.Format</c> resolves <c>:C</c> against
/// <see cref="CultureInfo.CurrentCulture"/> — the browser's culture — which silently bypasses
/// <c>CurrencyFormatter</c> and its en-CA formatting. Call sites must format the amount
/// first and pass the finished string.
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
            $"'{key}' must not format currency itself — the amount arrives pre-formatted from CurrencyFormatter");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// Cross-cutting regression guard for currency placeholders.
