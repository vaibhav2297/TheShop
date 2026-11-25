using Microsoft.Playwright;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Browser controls for .specs/003_manage-hero-banner/spec.md.</summary>
public sealed class ManageHeroBannerPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageHeroBanner;

    public ILocator Root => Page.GetByTestId("hero-banner-admin");
    public ILocator Heading => Page.GetByTestId("hero-heading");
    public ILocator Description => Page.GetByTestId("hero-description");
    public ILocator Desktop => Page.GetByTestId("hero-desktop-image");
    public ILocator Mobile => Page.GetByTestId("hero-mobile-image");
    public ILocator Save => Page.GetByTestId("hero-save");
    public ILocator Cancel => Page.GetByTestId("hero-cancel");

    public async Task SelectDesktopAsync(byte[] bytes, string name = "desktop.png") =>
        await Desktop.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        { Name = name, MimeType = "image/png", Buffer = bytes });

    public async Task SelectMobileAsync(byte[] bytes, string name = "mobile.png") =>
        await Mobile.Locator("input[type=file]").SetInputFilesAsync(new FilePayload
        { Name = name, MimeType = "image/png", Buffer = bytes });

}
