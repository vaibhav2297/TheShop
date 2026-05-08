using MudBlazor;

namespace TheShop.Web.Theme;

public class ShopTheme
{
    public MudTheme BuildTheme()
    {
        return new MudTheme
        {
            PaletteLight  = BuildLightPalette(),
            PaletteDark   = BuildDarkPalette(),
            Typography    = BuildTypography(),
            LayoutProperties = BuildLayout(),
        };
    }

    private PaletteLight BuildLightPalette() => new()
    {
        Primary          = ShopColors.Primary,
        // TODO: Uncomment and set remaining tokens once Figma values are finalized
        // Secondary     = ShopColors.Secondary,
        // Tertiary      = ShopColors.Tertiary,
        // Background    = ShopColors.Background,
        // Surface       = ShopColors.Surface,
        // AppbarBackground = ShopColors.Background,
        // DrawerBackground = ShopColors.Surface,
        // TextPrimary   = ShopColors.TextPrimary,
        // TextSecondary = ShopColors.TextSecondary,
        // Success       = ShopColors.Success,
        // Warning       = ShopColors.Warning,
        // Error         = ShopColors.Error,
        // Info          = ShopColors.Info,
        // LinesDefault  = ShopColors.BorderSecondary,
        // LinesInputs   = ShopColors.BorderSecondary,
    };

    private PaletteDark BuildDarkPalette() => new()
    {
        Primary = ShopColors.Primary,
        // TODO: Uncomment once dark-mode Figma tokens are finalized
        // Background    = ShopColors.Dark.Background,
        // Surface       = ShopColors.Dark.Surface,
        // TextPrimary   = ShopColors.Dark.TextPrimary,
        // TextSecondary = ShopColors.Dark.TextSecondary,
    };

    private Typography BuildTypography() => new()
    {
        // TODO: Wire ShopTypography tokens once Figma values are finalized.
        // Example:
        // Default = new DefaultTypography
        // {
        //     FontFamily = [ShopTypography.FontFamilyPrimary],
        //     FontSize   = ShopTypography.Body1_Size,
        //     LineHeight = ShopTypography.Body1_LineHeight,
        //     FontWeight = ShopTypography.WeightRegular,
        // },
    };

    private LayoutProperties BuildLayout() => new()
    {
        DefaultBorderRadius = "8px",
        AppbarHeight        = "64px",
    };
}
