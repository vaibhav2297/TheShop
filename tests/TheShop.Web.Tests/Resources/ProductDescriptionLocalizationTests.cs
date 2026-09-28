using System.Globalization;
using FluentAssertions;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Resources;

/// <summary>
/// Tests that every new product-description UI string has a real English resource
/// entry (FR-8, AC-12) — no <c>[TODO]</c> placeholder, since AC-12 is a stated acceptance
/// criterion rather than a review-gate cleanup.
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ProductDescriptionLocalizationTests
{

    private static readonly string[] Keys =
    [
        "ProductContent_Heading",
        "AddProduct_DescriptionCounter",
        "AddProduct_DescriptionFormattingRemoved",
        "ProductSpecifications_Heading",
        "ProductSpecifications_AddRow",
        "ProductSpecifications_NameLabel",
        "ProductSpecifications_ValueLabel",
        "ProductSpecifications_RemoveRow",
        "ProductSpecifications_RowRemovedAnnouncement",
        "ProductSpecifications_EmptyStateTitle",
        "ProductSpecifications_EmptyStateDescription",
        "Product_DescriptionTooLong",
        "Product_DescriptionUnsupportedContent",
        "Product_DescriptionTooLarge",
        "Product_SpecificationNameRequired",
        "Product_SpecificationValueRequired",
        "Product_SpecificationNameDuplicated",
    ];

    public static IEnumerable<object[]> KeysData() => Keys.Select(key => (object[])[key]);

    [Theory]
    [MemberData(nameof(KeysData))]
    [Trait("Feature", "product-description")]
    public void UiString_ForEveryNewKey_IsAvailableInEnglish(string key)
    {
        var english = Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture);

        english.Should().NotBeNullOrWhiteSpace($"'{key}' must have an English resource string");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void DescriptionCounterFormat_HasOnePlaceholderForCountAndOneForTheMaximum()
    {
        var formatted = string.Format(Strings.AddProduct_DescriptionCounter, 5, 20_000);

        formatted.Should().Contain("5").And.Contain("20000");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-12 (English interface text for every new control/message): UiString_ForEveryNewKey_IsAvailableInEnglish
