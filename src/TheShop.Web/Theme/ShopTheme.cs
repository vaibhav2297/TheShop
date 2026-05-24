using MudBlazor;

namespace TheShop.Web.Theme;

public class ShopTheme
{
    public static MudTheme BuildTheme()
    {
        return new MudTheme
        {
            PaletteLight     = BuildLightPalette(),
            PaletteDark      = BuildDarkPalette(),
            Typography       = BuildTypography(),
            LayoutProperties = BuildLayout(),
        };
    }

    private static PaletteLight BuildLightPalette() => new()
    {
        Primary               = ShopColors.Primary,
        PrimaryContrastText   = ShopColors.PrimaryContrast,
        Secondary             = ShopColors.Secondary,
        SecondaryContrastText = ShopColors.SecondaryContrast,
        Tertiary              = ShopColors.Tertiary,
        TertiaryContrastText  = ShopColors.TertiaryContrast,
        Background            = ShopColors.Background,
        Surface               = ShopColors.Surface,
        AppbarBackground      = ShopColors.AppbarBackground,
        DrawerBackground      = ShopColors.DrawerBackground,
        TextPrimary           = ShopColors.TextPrimary,
        TextSecondary         = ShopColors.TextSecondary,
        TextDisabled          = ShopColors.TextDisabled,
        Success               = ShopColors.Success,
        Warning               = ShopColors.Warning,
        Error                 = ShopColors.Error,
        Info                  = ShopColors.Info,
        LinesDefault          = ShopColors.LinesDefault,
        LinesInputs           = ShopColors.LinesInput,
        Divider               = ShopColors.Divider,
    };

    private static PaletteDark BuildDarkPalette() => new()
    {
        Primary       = ShopColors.Primary,
        Background    = ShopColors.Dark.Background,
        Surface       = ShopColors.Dark.Surface,
        TextPrimary   = ShopColors.Dark.TextPrimary,
        TextSecondary = ShopColors.Dark.TextSecondary,
    };

    private static Typography BuildTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Body1_Size,
            FontWeight    = ShopTypography.Body1_Weight,
            LineHeight    = ShopTypography.Body1_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H1 = new H1Typography
        {
            FontFamily    = [ShopTypography.FontFamilyHeading, "sans-serif"],
            FontSize      = ShopTypography.H1_Size,
            FontWeight    = ShopTypography.H1_Weight,
            LineHeight    = ShopTypography.H1_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H2 = new H2Typography
        {
            FontFamily    = [ShopTypography.FontFamilyHeading, "sans-serif"],
            FontSize      = ShopTypography.H2_Size,
            FontWeight    = ShopTypography.H2_Weight,
            LineHeight    = ShopTypography.H2_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H3 = new H3Typography
        {
            FontFamily    = [ShopTypography.FontFamilyHeading, "sans-serif"],
            FontSize      = ShopTypography.H3_Size,
            FontWeight    = ShopTypography.H3_Weight,
            LineHeight    = ShopTypography.H3_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H4 = new H4Typography
        {
            FontFamily    = [ShopTypography.FontFamilyHeading, "sans-serif"],
            FontSize      = ShopTypography.H4_Size,
            FontWeight    = ShopTypography.H4_Weight,
            LineHeight    = ShopTypography.H4_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H5 = new H5Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.H5_Size,
            FontWeight    = ShopTypography.H5_Weight,
            LineHeight    = ShopTypography.H5_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        H6 = new H6Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.H6_Size,
            FontWeight    = ShopTypography.H6_Weight,
            LineHeight    = ShopTypography.H6_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Subtitle1 = new Subtitle1Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Subtitle1_Size,
            FontWeight    = ShopTypography.Subtitle1_Weight,
            LineHeight    = ShopTypography.Subtitle1_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Subtitle2 = new Subtitle2Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Subtitle2_Size,
            FontWeight    = ShopTypography.Subtitle2_Weight,
            LineHeight    = ShopTypography.Subtitle2_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Body1 = new Body1Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Body1_Size,
            FontWeight    = ShopTypography.Body1_Weight,
            LineHeight    = ShopTypography.Body1_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Body2 = new Body2Typography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Body2_Size,
            FontWeight    = ShopTypography.Body2_Weight,
            LineHeight    = ShopTypography.Body2_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Button = new ButtonTypography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Button_Size,
            FontWeight    = ShopTypography.Button_Weight,
            LineHeight    = ShopTypography.Button_LineHeight,
            TextTransform = "capitalize",
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Caption = new CaptionTypography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Caption_Size,
            FontWeight    = ShopTypography.Caption_Weight,
            LineHeight    = ShopTypography.Caption_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
        Overline = new OverlineTypography
        {
            FontFamily    = [ShopTypography.FontFamilyPrimary, "sans-serif"],
            FontSize      = ShopTypography.Overline_Size,
            FontWeight    = ShopTypography.Overline_Weight,
            LineHeight    = ShopTypography.Overline_LineHeight,
            LetterSpacing = ShopTypography.LetterSpacing,
        },
    };

    private static LayoutProperties BuildLayout() => new()
    {
        DefaultBorderRadius = "0px",
    };
}
