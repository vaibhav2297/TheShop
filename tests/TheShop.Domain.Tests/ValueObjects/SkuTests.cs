using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for the <see cref="Sku"/> value object: creation/trimming, the store-wide comparison
/// key (RULE-8), and the two <see cref="Sku.Suggest(string)"/> overloads that generate a
/// product's and a variant's SKU automatically (FR-15, Decision 7/11 — a SKU is never typed or
/// edited directly).
/// <see href=".specs/create-product/spec.md"/>
/// </summary>
public class SkuTests
{
    // =========================================================================
    // Create (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_TrimsSurroundingWhitespace()
    {
        var sku = Sku.Create("  ELF-BC5000  ");

        sku.Value.Should().Be("ELF-BC5000");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Feature", "create-product")]
    public void Create_WithBlankValue_ThrowsSkuRequiredException(string value)
    {
        var act = () => Sku.Create(value);

        act.Should().Throw<SkuRequiredException>();
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Create_WithNull_ThrowsSkuRequiredException()
    {
        var act = () => Sku.Create(null!);

        act.Should().Throw<SkuRequiredException>();
    }

    // =========================================================================
    // Normalized / Equals — the store-wide comparison key (RULE-8)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void Equals_WithDifferentCaseAndSurroundingSpaces_ReturnsTrue()
    {
        var first = Sku.Create("ELF-BC5000");
        var second = Sku.Create("  elf-bc5000  ");

        first.Equals(second).Should().BeTrue("two SKUs differing only by case or surrounding whitespace collide (RULE-8)");
        first.Normalized.Should().Be(second.Normalized);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void Equals_WithDifferentSkus_ReturnsFalse()
    {
        var first = Sku.Create("ELF-BC5000");
        var second = Sku.Create("ELF-BC6000");

        first.Equals(second).Should().BeFalse();
    }

    // =========================================================================
    // Suggest(productName) — the product-level SKU suggestion (Decision 7, FR-6, AC-11)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromName_UppercasesAndHyphenatesTheName()
    {
        var sku = Sku.Suggest("Elf Bar, BC5000");

        sku.Value.Should().Be("ELF-BAR-BC5000");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromName_WithABlankName_FallsBackToTheSkuPlaceholder()
    {
        var sku = Sku.Suggest("   ");

        sku.Value.Should().Be("SKU");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromName_WhenTheNameChanges_ProducesADifferentSku()
    {
        var first = Sku.Suggest("Elf Bar BC5000");
        var second = Sku.Suggest("Elf Bar BC10000");

        first.Should().NotBe(second, "FR-15: the SKU stays live and regenerates as the name changes");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromName_TwoDifferentlyPunctuatedNames_CanCollide()
    {
        // Slugification is lossy by design (plan Decision 7) — the backstop is the ordinary
        // SKU-conflict refusal, not a disambiguation mechanism here.
        var first = Sku.Suggest("Elf Bar, BC5000");
        var second = Sku.Suggest("Elf Bar BC5000");

        first.Should().Be(second);
    }

    // =========================================================================
    // Suggest(productSku, optionValues) — the variant-level SKU suggestion (FR-15)
    // =========================================================================

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromProductSkuAndValues_BuildsTheHyphenatedSuffixForm()
    {
        var productSku = Sku.Create("ELF-BC5000");

        var variantSku = Sku.Suggest(productSku, ["Mango", "20mg"]);

        variantSku.Value.Should().Be("ELF-BC5000-MANGO-20MG");
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromProductSkuAndValues_WithNoValues_ReturnsTheProductSkuUnchanged()
    {
        var productSku = Sku.Create("ELF-BC5000");

        var variantSku = Sku.Suggest(productSku, []);

        variantSku.Should().Be(productSku);
    }

    [Fact]
    [Trait("Feature", "create-product")]
    public void SuggestFromProductSkuAndValues_WhenAnOptionValueIsRenamed_RegeneratesTheSuffix()
    {
        var productSku = Sku.Create("ELF-BC5000");

        var before = Sku.Suggest(productSku, ["Mango"]);
        var after = Sku.Suggest(productSku, ["Mango!"]);

        before.Should().Be(after, "punctuation is stripped during slugification, so this pair does collide — " +
            "the ordinary SKU-conflict refusal is the backstop for that rare case (Decision 7)");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-11 (SKU generated automatically from the name, live, no direct edit): SuggestFromName_UppercasesAndHyphenatesTheName,
//        SuggestFromName_WhenTheNameChanges_ProducesADifferentSku, SuggestFromProductSkuAndValues_BuildsTheHyphenatedSuffixForm,
//        SuggestFromProductSkuAndValues_WhenAnOptionValueIsRenamed_RegeneratesTheSuffix
// AC-27 (SKU conflict — the lossy-slugification backstop): SuggestFromName_TwoDifferentlyPunctuatedNames_CanCollide
// RULE-8 (store-wide SKU comparison, case/space insensitive): Create_TrimsSurroundingWhitespace, Equals_WithDifferentCaseAndSurroundingSpaces_ReturnsTrue,
//        Equals_WithDifferentSkus_ReturnsFalse, Create_WithBlankValue_ThrowsSkuRequiredException, Create_WithNull_ThrowsSkuRequiredException
