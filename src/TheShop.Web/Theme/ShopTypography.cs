namespace TheShop.Web.Theme;

public static class ShopTypography
{
    // Font families — two typefaces from Figma text styles
    public const string FontFamilyHeading = "Barlow Condensed"; // H1–H4
    public const string FontFamilyPrimary = "Space Grotesk";    // H5–Overline + Default

    // Font weights (string — matches MudBlazor 9 BaseTypography.FontWeight)
    public const string WeightRegular   = "400";
    public const string WeightMedium    = "500";
    public const string WeightBold      = "700";
    public const string WeightExtraBold = "800";

    // Letter spacing — all Figma text styles use 0.25 px (uniform across the scale)
    public const string LetterSpacing = "0.25px";

    // H1 — Barlow Condensed ExtraBold 96px
    public const string H1_Size       = "6rem";
    public const string H1_Weight     = WeightExtraBold;
    public const string H1_LineHeight = "1.167";

    // H2 — Barlow Condensed ExtraBold 60px
    public const string H2_Size       = "3.75rem";
    public const string H2_Weight     = WeightExtraBold;
    public const string H2_LineHeight = "1.2";

    // H3 — Barlow Condensed ExtraBold 48px
    public const string H3_Size       = "3rem";
    public const string H3_Weight     = WeightExtraBold;
    public const string H3_LineHeight = "1.167";

    // H4 — Barlow Condensed Bold 34px
    public const string H4_Size       = "2.125rem";
    public const string H4_Weight     = WeightBold;
    public const string H4_LineHeight = "1.235";

    // H5 — Space Grotesk Regular 24px
    public const string H5_Size       = "1.5rem";
    public const string H5_Weight     = WeightRegular;
    public const string H5_LineHeight = "1.334";

    // H6 — Space Grotesk Medium 20px
    public const string H6_Size       = "1.25rem";
    public const string H6_Weight     = WeightMedium;
    public const string H6_LineHeight = "1.6";

    // Subtitle 1 — Space Grotesk Medium 16px
    public const string Subtitle1_Size       = "1rem";
    public const string Subtitle1_Weight     = WeightMedium;
    public const string Subtitle1_LineHeight = "1.75";

    // Subtitle 2 — Space Grotesk Medium 14px
    public const string Subtitle2_Size       = "0.875rem";
    public const string Subtitle2_Weight     = WeightMedium;
    public const string Subtitle2_LineHeight = "1.57";

    // Body 1 — Space Grotesk Regular 16px
    public const string Body1_Size       = "1rem";
    public const string Body1_Weight     = WeightRegular;
    public const string Body1_LineHeight = "1.5";

    // Body 2 — Space Grotesk Regular 14px
    public const string Body2_Size       = "0.875rem";
    public const string Body2_Weight     = WeightRegular;
    public const string Body2_LineHeight = "1.43";

    // Button — Space Grotesk Medium 14px (title-case in Figma)
    public const string Button_Size       = "0.875rem";
    public const string Button_Weight     = WeightMedium;
    public const string Button_LineHeight = "1.75";

    // Caption — Space Grotesk Regular 12px
    public const string Caption_Size       = "0.75rem";
    public const string Caption_Weight     = WeightRegular;
    public const string Caption_LineHeight = "1.66";

    // Overline — Space Grotesk Regular 10px
    public const string Overline_Size       = "0.625rem";
    public const string Overline_Weight     = WeightRegular;
    public const string Overline_LineHeight = "2.66";
}
