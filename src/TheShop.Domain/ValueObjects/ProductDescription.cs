using System.Text.RegularExpressions;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// A product's rich-text description: whitelisted HTML markup (RULE-5) bounded by a
/// 20,000-character plain-text count (RULE-2). <see cref="Create"/> enforces both invariants
/// on a staff write; <see cref="Rehydrate"/> skips both so a persisted row always loads even
/// after a bound tightens.
/// </summary>
public sealed class ProductDescription
{
    /// <summary>
    /// The maximum plain-text length, counting spaces and each paragraph/line break once.
    /// </summary>
    public const int MaxTextLength = 20_000;

    /// <summary>
    /// The maximum raw HTML size in UTF-8 bytes. Enforced by the Application validator, not by
    /// this type — exposed here so callers share one source of the bound.
    /// </summary>
    public const int MaxHtmlBytes = 200_000;

    private static readonly Regex TagPattern = new("<[^>]*>", RegexOptions.Compiled);

    private static readonly Regex LineBreakTagPattern = new(
        @"</p>|</h[1-6]>|</li>|<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EntityPattern = new(
        "&amp;|&lt;|&gt;|&quot;|&#39;|&nbsp;", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AllowedStructuralTag = new(
        @"^</?(p|br|h[1-6]|strong|em|ol|ul|li)\s*/?>$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AllowedLinkClose = new(
        "^</a>$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AllowedLinkOpen = new(
        "^<a\\s+href=\"(https?://|mailto:)[^\"<>]*\"(\\s+target=\"_blank\"|\\s+rel=\"noopener noreferrer\")*\\s*>$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// The empty description — no HTML, no text, valid without a save round trip (RULE-2, AC-6).
    /// </summary>
    public static readonly ProductDescription Empty = new(string.Empty, string.Empty);

    /// <summary>
    /// The whitelisted HTML markup, exactly as authored.
    /// </summary>
    public string Html { get; }

    /// <summary>
    /// The text with every tag stripped, line-break tags mapped to <c>\n</c>, and HTML entities
    /// decoded — what <see cref="TextLength"/> counts.
    /// </summary>
    public string PlainText { get; }

    /// <summary>
    /// The plain-text character count RULE-2 bounds at <see cref="MaxTextLength"/>.
    /// </summary>
    public int TextLength => PlainText.Length;

    /// <summary>
    /// <c>true</c> when the description carries no plain text.
    /// </summary>
    public bool IsEmpty => TextLength == 0;

    private ProductDescription(string html, string plainText)
    {
        Html = html;
        PlainText = plainText;
    }

    /// <summary>
    /// Validates and builds a <see cref="ProductDescription"/> from staff-authored HTML,
    /// enforcing the whitelisted markup grammar and the plain-text length bound. A
    /// <c>null</c>/empty value, or content whose plain text is blank, normalizes to
    /// <see cref="Empty"/> (AC-6).
    /// </summary>
    /// <exception cref="ProductDescriptionUnsupportedContentException">
    /// <paramref name="html"/> contains a tag or attribute outside the whitelisted grammar.
    /// </exception>
    /// <exception cref="ProductDescriptionTooLongException">
    /// The plain-text length exceeds <see cref="MaxTextLength"/>.
    /// </exception>
    public static ProductDescription Create(string? html)
    {
        var trimmedHtml = html?.Trim() ?? string.Empty;
        if (trimmedHtml.Length == 0)
            return Empty;

        EnsureAllowedGrammar(trimmedHtml);

        var plainText = ComputePlainText(trimmedHtml);
        if (plainText.Length == 0)
            return Empty;

        if (plainText.Length > MaxTextLength)
            throw new ProductDescriptionTooLongException();

        return new ProductDescription(trimmedHtml, plainText);
    }

    /// <summary>
    /// Reconstructs a <see cref="ProductDescription"/> from a persisted row without re-running
    /// grammar or length checks — a stored row must always load.
    /// </summary>
    public static ProductDescription Rehydrate(string html)
    {
        var trimmedHtml = html?.Trim() ?? string.Empty;
        return trimmedHtml.Length == 0
            ? Empty
            : new ProductDescription(trimmedHtml, ComputePlainText(trimmedHtml));
    }

    private static void EnsureAllowedGrammar(string html)
    {
        foreach (Match match in TagPattern.Matches(html))
        {
            var tag = match.Value;
            if (AllowedStructuralTag.IsMatch(tag) || AllowedLinkClose.IsMatch(tag) || AllowedLinkOpen.IsMatch(tag))
                continue;

            throw new ProductDescriptionUnsupportedContentException();
        }
    }

    private static string ComputePlainText(string html)
    {
        var withBreaks = LineBreakTagPattern.Replace(html, "\n");
        var stripped = TagPattern.Replace(withBreaks, string.Empty);
        var decoded = EntityPattern.Replace(stripped, DecodeEntity);
        return decoded.Trim();
    }

    private static string DecodeEntity(Match match) => match.Value.ToLowerInvariant() switch
    {
        "&amp;" => "&",
        "&lt;" => "<",
        "&gt;" => ">",
        "&quot;" => "\"",
        "&#39;" => "'",
        "&nbsp;" => " ",
        _ => match.Value,
    };
}
