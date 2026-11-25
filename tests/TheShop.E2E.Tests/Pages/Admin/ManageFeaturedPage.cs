using Microsoft.Playwright;
using TheShop.Web.Resources;
using WebRoutes = TheShop.Web.Common.Routes;

namespace TheShop.E2E.Tests.Pages.Admin;

/// <summary>Browser controls and geometry for .specs/005_homepage-featured/spec.md.</summary>
public sealed class ManageFeaturedPage(IPage page) : ShopPage(page)
{
    protected override string Route => WebRoutes.Admin.ManageFeatured;
    public ILocator Root => Page.GetByTestId("featured-admin");
    public ILocator Title => Page.GetByTestId("featured-title");
    public ILocator Save => Page.GetByTestId("featured-save");
    public ILocator Cancel => Page.GetByTestId("featured-cancel");
    public ILocator Reload => Page.GetByTestId("featured-reload");
    public ILocator Settings => Page.GetByTestId("featured-settings");
    public ILocator Scroll => Page.Locator("main[data-shop-scroll-root]");
    public ILocator Tile(int number) => Page.GetByTestId($"featured-tile-{number}");
    public ILocator Image(int number) => Tile(number).GetByTestId("featured-image");
    public ILocator TileTitle(int number) => Tile(number).GetByTestId("featured-tile-title");
    public ILocator Label(int number) => Tile(number).GetByTestId("featured-label");
    public ILocator ButtonLabel(int number) => Tile(number).GetByTestId("featured-button-label");
    public ILocator ButtonLink(int number) => Tile(number).GetByTestId("featured-button-link");
    public ILocator Status(bool visible) => Page.GetByTestId(visible ? "featured-visible" : "featured-hidden");

    public Task SelectAsync(int number, byte[] bytes, string mime = "image/png", string name = "tile.png") =>
        Image(number).Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = name, MimeType = mime, Buffer = bytes });

    public async Task SelectValidAsync(int number, byte[] bytes, string mime = "image/png", string name = "tile.png")
    {
        var prior = await Image(number).Locator("img").CountAsync() == 0 ? null
            : await Image(number).Locator("img").GetAttributeAsync("src");
        await SelectAsync(number, bytes, mime, name);
        await Assertions.Expect(Image(number).Locator("img")).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex("^blob:"));
        if (prior is not null) await Assertions.Expect(Image(number).Locator("img")).Not.ToHaveAttributeAsync("src", prior);
        await Image(number).Locator("img").EvaluateAsync("el => el.decode()");
        await Assertions.Expect(Image(number)).ToHaveAttributeAsync("data-upload-disabled", "false");
    }

    public async Task FillValidAsync(string title, byte[] image)
    {
        await Title.FillAsync(title);
        for (var number = 1; number <= 2; number++)
        {
            await TileTitle(number).FillAsync($"{title} tile {number}");
            await SelectValidAsync(number, image);
        }
    }

    public async Task SaveAndReturnAsync()
    {
        await Save.ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin/homepage$"));
    }

    public async Task OpenFromHomepageAsync()
    {
        await Page.GotoAsync(WebRoutes.Admin.ManageHomepage);
        await Page.GetByTestId("manage-featured").ClickAsync();
        await Assertions.Expect(Root).ToBeVisibleAsync();
    }

    public async Task ScrollToAsync(int offset)
    {
        await Scroll.EvaluateAsync("(el,y) => el.scrollTo({top:y,behavior:'instant'})", offset);
        await SettleLayoutAsync();
    }

    public Task SettleLayoutAsync() => Page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");

    public Task WaitForStickyOffsetAsync() => Page.WaitForFunctionAsync("""
        () => { const panel = document.querySelector('[data-testid="featured-settings"]');
            const scroll = document.querySelector('main[data-shop-scroll-root]');
            return panel && Math.abs(parseFloat(getComputedStyle(panel).top) - Math.min(0, scroll.clientHeight - panel.getBoundingClientRect().height)) < 1; }
        """);

    public Task<byte[]> ArtworkAsync(string mime = "image/png", int width = 1200, int height = 1200, string color = "#c83232") =>
        ArtworkCoreAsync(mime, width, height, color);

    private async Task<byte[]> ArtworkCoreAsync(string mime, int width, int height, string color)
    {
        var base64 = await Page.EvaluateAsync<string>("""
            args => { const canvas = document.createElement('canvas'); canvas.width = args.width; canvas.height = args.height;
                const ctx = canvas.getContext('2d'); ctx.fillStyle = args.color; ctx.fillRect(0,0,args.width,args.height);
                return canvas.toDataURL(args.mime).split(',')[1]; }
            """, new { mime, width, height, color });
        return Convert.FromBase64String(base64);
    }

    public async Task AssertHealthyAsync()
    {
        await Assertions.Expect(Page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
        await Assertions.Expect(Root).ToBeVisibleAsync();
    }
}
