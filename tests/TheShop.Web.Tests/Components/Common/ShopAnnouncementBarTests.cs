using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopAnnouncementBarTests : TestContext
{
    [Fact]
    public void Render_ShowsEncodedMessageInNativeMarkup()
    {
        var cut = Render<ShopAnnouncementBar>(p => p
            .Add(x => x.Message, "Free shipping <script>unsafe</script>"));

        var root = cut.Find("div");
        root.ClassName.Should().Be("shop-native shop-announcement-bar");
        var message = root.QuerySelector("p.shop-announcement-bar-message")!;
        message.TextContent.Should().Be("Free shipping <script>unsafe</script>");
        cut.FindAll("script").Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Fact]
    public void Render_ForwardsClassStyleAndAttributesToRoot()
    {
        var cut = Render<ShopAnnouncementBar>(p => p
            .Add(x => x.Message, "Sale")
            .Add(x => x.Class, "extra")
            .Add(x => x.Style, "--probe: 1")
            .AddUnmatched("data-testid", "announcement"));

        var root = cut.Find("[data-testid=announcement]");
        root.ClassName.Should().Be("shop-native shop-announcement-bar extra");
        root.GetAttribute("style").Should().Be("--probe: 1");
    }
}
