using Bunit;
using FluentAssertions;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using TheShop.Web.Theme;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopPaginationTests : TestContext
{
    [Theory]
    [InlineData(3, 5, "1,2,3,4,5", 0)]
    [InlineData(3, 20, "1,2,3,4,5,20", 1)]
    [InlineData(10, 20, "1,9,10,11,20", 2)]
    [InlineData(18, 20, "1,16,17,18,19,20", 1)]
    [InlineData(4, 7, "1,2,3,4,5,6,7", 0)]
    [InlineData(5, 8, "1,4,5,6,7,8", 1)]
    [InlineData(int.MaxValue, int.MaxValue, "1,2147483643,2147483644,2147483645,2147483646,2147483647", 1)]
    public void PageWindow_MatchesFigmaRangesAndRemainsBounded(int page, int total, string expected, int ellipses)
    {
        var cut = Render<ShopPagination>(p => p.Add(x => x.Page, page).Add(x => x.TotalPages, total));
        string.Join(",", cut.FindAll("button:not(.shop-pagination-arrow)").Select(x => x.TextContent.Trim())).Should().Be(expected);
        cut.FindAll(".shop-pagination-ellipsis").Should().HaveCount(ellipses)
            .And.OnlyContain(x => x.GetAttribute("aria-hidden") == "true");
        cut.FindComponents<ShopIcon>().Count(x => x.Instance.Icon == ShopIcons.Outlined.More_Horizontal)
            .Should().Be(ellipses);
        foreach (var ellipsis in cut.FindAll(".shop-pagination-ellipsis"))
        {
            ellipsis.TagName.Should().Be("SPAN");
            ellipsis.TextContent.Should().BeNullOrWhiteSpace();
            ellipsis.HasAttribute("tabindex").Should().BeFalse();
            var icon = ellipsis.QuerySelector("svg.shop-pagination-icon")!;
            icon.GetAttribute("aria-hidden").Should().Be("true");
            icon.GetAttribute("focusable").Should().Be("false");
        }
        cut.Find("[aria-current='page']").TextContent.Should().Be(page.ToString());
        cut.Find("nav").GetAttribute("aria-label").Should().Be(Strings.Pagination_Label);
    }

    [Fact]
    public void Selection_PreservesCallbackContractAndGuardsDisabledAndBoundaries()
    {
        var received = new List<int>();
        var cut = Render<ShopPagination>(p => p.Add(x => x.Page, 1).Add(x => x.TotalPages, 5)
            .Add(x => x.PageChanged, page => received.Add(page)).Add(x => x.Class, "custom")
            .AddUnmatched("data-probe", "pages"));
        cut.Find("nav").ClassList.Should().Contain("custom");
        cut.Find("nav").GetAttribute("data-probe").Should().Be("pages");
        cut.Find("[aria-current]").Click();
        cut.Find(".shop-pagination-arrow").Click();
        received.Should().BeEmpty();
        cut.FindAll("button:not(.shop-pagination-arrow)")[2].Click();
        received.Should().Equal(3);
        cut.Find("[aria-current]").TextContent.Should().Be("1");
        cut.FindAll(".shop-pagination-arrow")[1].Click();
        received.Should().Equal(3, 2);
        cut.Render(p => p.Add(x => x.Page, 5).Add(x => x.Disabled, true));
        cut.FindAll("button").Should().OnlyContain(x => x.HasAttribute("disabled"));
        cut.FindAll("button:not(.shop-pagination-arrow)")[0].Click();
        received.Should().Equal(3, 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EmptyTotals_RenderNoNavigation(int total)
    {
        var cut = Render<ShopPagination>(p => p.Add(x => x.TotalPages, total));
        cut.FindAll("nav").Should().BeEmpty();
    }

    [Theory]
    [InlineData(-20, 1)]
    [InlineData(40, 5)]
    public void OutOfRangePage_ClampsPresentationWithoutCallback(int page, int expected)
    {
        var calls = 0;
        var cut = Render<ShopPagination>(p => p.Add(x => x.Page, page).Add(x => x.TotalPages, 5)
            .Add(x => x.PageChanged, _ => calls++));
        cut.Find("[aria-current]").TextContent.Should().Be(expected.ToString());
        calls.Should().Be(0);
    }

    [Fact]
    public void SinglePage_DisablesBothArrows()
    {
        var cut = Render<ShopPagination>();
        cut.FindAll(".shop-pagination-arrow").Should().OnlyContain(x => x.HasAttribute("disabled"));
        cut.FindAll("[aria-current='page']").Should().ContainSingle();
    }
}
