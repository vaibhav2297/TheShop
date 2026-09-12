using FluentAssertions;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;
using Xunit;

namespace TheShop.Domain.Tests.ValueObjects;

/// <summary>
/// Tests for <see cref="ProductDescription"/>: the Decision 3 whitelist markup grammar
/// (RULE-5, FR-7, AC-10, AC-11), the Decision 5 plain-text counting rule bounded at
/// <see cref="ProductDescription.MaxTextLength"/> (RULE-2, FR-3, AC-4, AC-5), and
/// <see cref="ProductDescription.Rehydrate"/>'s no-revalidation contract for persisted rows
/// (Decision 8, AC-3).
/// <see href=".specs/product-description/spec.md"/>
/// </summary>
public class ProductDescriptionTests
{
    // =========================================================================
    // Create — empty/blank normalizes to Empty (RULE-2, AC-6)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithNull_ReturnsEmpty()
    {
        var description = ProductDescription.Create(null);

        description.Should().BeSameAs(ProductDescription.Empty);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithEmptyString_ReturnsEmpty()
    {
        var description = ProductDescription.Create(string.Empty);

        description.Should().BeSameAs(ProductDescription.Empty);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithWhitespaceOnlyHtml_ReturnsEmpty()
    {
        var description = ProductDescription.Create("   ");

        description.Should().BeSameAs(ProductDescription.Empty);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithMarkupThatCarriesNoVisibleText_NormalizesToEmpty()
    {
        // Quill's empty document is "<p><br></p>" — trimmed plain text is blank (AC-6).
        var description = ProductDescription.Create("<p><br></p>");

        description.IsEmpty.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Empty_HasZeroTextLength()
    {
        ProductDescription.Empty.TextLength.Should().Be(0);
        ProductDescription.Empty.IsEmpty.Should().BeTrue();
    }

    // =========================================================================
    // Create — every FR-1 supported format is accepted (grammar whitelist)
    // =========================================================================

    [Theory]
    [InlineData("<p>Paragraph</p>")]
    [InlineData("Text<br>Break")]
    [InlineData("Text<br/>Break")]
    [InlineData("Text<br />Break")]
    [InlineData("<h1>H1</h1>")]
    [InlineData("<h2>H2</h2>")]
    [InlineData("<h3>H3</h3>")]
    [InlineData("<h4>H4</h4>")]
    [InlineData("<h5>H5</h5>")]
    [InlineData("<h6>H6</h6>")]
    [InlineData("<strong>Bold</strong>")]
    [InlineData("<em>Italic</em>")]
    [InlineData("<ol><li>Item</li></ol>")]
    [InlineData("<ul><li>Item</li></ul>")]
    [InlineData("<a href=\"https://example.com\">Link</a>")]
    [InlineData("<a href=\"http://example.com\">Link</a>")]
    [InlineData("<a href=\"mailto:staff@example.com\">Email</a>")]
    [InlineData("<a href=\"https://example.com\" target=\"_blank\">Link</a>")]
    [InlineData("<a href=\"https://example.com\" rel=\"noopener noreferrer\">Link</a>")]
    [InlineData("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">Link</a>")]
    [InlineData("<a href=\"https://example.com\" rel=\"noopener noreferrer\" target=\"_blank\">Link</a>")]
    [Trait("Feature", "product-description")]
    public void Create_WithEverySupportedFormat_DoesNotThrow(string html)
    {
        var act = () => ProductDescription.Create(html);

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_MatchesTagsCaseInsensitively()
    {
        var act = () => ProductDescription.Create("<P>Text</P><STRONG>Bold</STRONG>");

        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithPlainTextAndNoMarkup_KeepsHtmlAsAuthored()
    {
        var description = ProductDescription.Create("Just plain text.");

        description.Html.Should().Be("Just plain text.");
        description.PlainText.Should().Be("Just plain text.");
    }

    // =========================================================================
    // Create — unsupported markup is rejected, never rewritten (RULE-5, AC-10, AC-11)
    // =========================================================================

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=\"x.png\">")]
    [InlineData("<table><tr><td>Cell</td></tr></table>")]
    [InlineData("<span>Text</span>")]
    [InlineData("<div>Text</div>")]
    [InlineData("<u>Underline</u>")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>")]
    [InlineData("<video src=\"x.mp4\"></video>")]
    [Trait("Feature", "product-description")]
    public void Create_WithADisallowedTag_ThrowsProductDescriptionUnsupportedContentException(string html)
    {
        var act = () => ProductDescription.Create(html);

        act.Should().Throw<ProductDescriptionUnsupportedContentException>();
    }

    [Theory]
    [InlineData("<p class=\"fancy\">Text</p>")]
    [InlineData("<strong style=\"color:red\">Bold</strong>")]
    [InlineData("<p onclick=\"doEvil()\">Text</p>")]
    [Trait("Feature", "product-description")]
    public void Create_WithADisallowedAttributeOnAnAllowedTag_ThrowsProductDescriptionUnsupportedContentException(string html)
    {
        var act = () => ProductDescription.Create(html);

        act.Should().Throw<ProductDescriptionUnsupportedContentException>();
    }

    [Theory]
    [InlineData("<a href=\"javascript:alert(1)\">Click</a>")]
    [InlineData("<a href=\"JavaScript:alert(1)\">Click</a>")]
    [InlineData("<a href=\"data:text/html,evil\">Click</a>")]
    [Trait("Feature", "product-description")]
    public void Create_WithALinkTargetingExecutableContent_ThrowsProductDescriptionUnsupportedContentException(string html)
    {
        var act = () => ProductDescription.Create(html);

        act.Should().Throw<ProductDescriptionUnsupportedContentException>(
            "supplied code must never execute during editing or reopening (RULE-5, AC-11)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithALinkMissingHref_ThrowsProductDescriptionUnsupportedContentException()
    {
        var act = () => ProductDescription.Create("<a>Click</a>");

        act.Should().Throw<ProductDescriptionUnsupportedContentException>();
    }

    // =========================================================================
    // Create — TextLength counting rule (Decision 5, RULE-2, FR-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_CountsEachParagraphBreakAsOneCharacter()
    {
        var description = ProductDescription.Create("<p>One</p><p>Two</p>");

        // "One" + "\n" + "Two", trimmed -> "One\nTwo" (7 characters).
        description.PlainText.Should().Be("One\nTwo");
        description.TextLength.Should().Be(7);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_DoesNotCountFormattingTagsTowardTextLength()
    {
        var withFormatting = ProductDescription.Create("<p><strong>Bold</strong> <em>Italic</em></p>");
        var withoutFormatting = ProductDescription.Create("<p>Bold Italic</p>");

        withFormatting.TextLength.Should().Be(withoutFormatting.TextLength);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_DoesNotCountALinksDestinationTowardTextLength()
    {
        var longHref = "https://example.com/" + new string('a', 500);

        var description = ProductDescription.Create($"<a href=\"{longHref}\">Link</a>");

        description.TextLength.Should().Be(4, "FR-3 excludes link destinations from the text allowance");
    }

    [Theory]
    [InlineData("&amp;", "&")]
    [InlineData("&lt;", "<")]
    [InlineData("&gt;", ">")]
    [InlineData("&quot;", "\"")]
    [InlineData("&#39;", "'")]
    [InlineData("&nbsp;", " ")]
    [Trait("Feature", "product-description")]
    public void Create_DecodesEachEntityToOneCharacter(string entity, string decoded)
    {
        var description = ProductDescription.Create($"<p>a{entity}b</p>");

        description.PlainText.Should().Be($"a{decoded}b");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_WithExactly20000Characters_Succeeds()
    {
        var html = new string('a', 20_000);

        var description = ProductDescription.Create(html);

        description.TextLength.Should().Be(20_000);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Create_With20001Characters_ThrowsProductDescriptionTooLongException()
    {
        var html = new string('a', 20_001);

        var act = () => ProductDescription.Create(html);

        act.Should().Throw<ProductDescriptionTooLongException>();
    }

    // =========================================================================
    // Rehydrate — skips grammar and length checks for a persisted row (Decision 8, AC-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "product-description")]
    public void Rehydrate_WithNull_ReturnsEmpty()
    {
        var description = ProductDescription.Rehydrate(null!);

        description.Should().BeSameAs(ProductDescription.Empty);
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Rehydrate_WithHtmlOverTheTextLengthBound_DoesNotThrow()
    {
        var html = new string('a', 20_001);

        var act = () => ProductDescription.Rehydrate(html);

        act.Should().NotThrow("a stored row must always load, even if a bound tightens later (Decision 8)");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Rehydrate_WithLiteralAngleBracketsSurvivingAsEscapedText_KeepsThemAsText()
    {
        // The Decision 7 migration escapes legacy plain text to "&lt;"/"&gt;" before this ever
        // loads, so the round trip through Rehydrate must decode them back to literal characters
        // rather than reinterpreting them as markup (AC-3).
        var description = ProductDescription.Rehydrate("<p>Use &lt;tags&gt; carefully</p>");

        description.PlainText.Should().Be("Use <tags> carefully");
    }

    [Fact]
    [Trait("Feature", "product-description")]
    public void Rehydrate_PreservesMultipleParagraphBreaks()
    {
        var description = ProductDescription.Rehydrate("<p>First</p><p>Second</p><p>Third</p>");

        description.PlainText.Should().Be("First\nSecond\nThird");
    }
}

// =============================================================================
// AC → Test mapping
// =============================================================================
// AC-1: Create_WithEverySupportedFormat_DoesNotThrow
// AC-3 (legacy plain text with angle brackets survives open/save intact): Rehydrate_WithLiteralAngleBracketsSurvivingAsEscapedText_KeepsThemAsText,
//        Rehydrate_PreservesMultipleParagraphBreaks
// AC-4 (2,001 and exactly 20,000 characters both save): Create_WithExactly20000Characters_Succeeds
// AC-5 (20,001 characters rejected): Create_With20001Characters_ThrowsProductDescriptionTooLongException
// AC-6 (all description text removed, empty persists): Create_WithNull_ReturnsEmpty, Create_WithWhitespaceOnlyHtml_ReturnsEmpty,
//        Create_WithMarkupThatCarriesNoVisibleText_NormalizesToEmpty
// AC-10/AC-11 (unsupported formatting/executable content rejected): Create_WithADisallowedTag_ThrowsProductDescriptionUnsupportedContentException,
//        Create_WithADisallowedAttributeOnAnAllowedTag_ThrowsProductDescriptionUnsupportedContentException,
//        Create_WithALinkTargetingExecutableContent_ThrowsProductDescriptionUnsupportedContentException
