using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using TheShop.Web.Components.Common;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopSwipeCarouselTests : TestContext
{
    private readonly IJSObjectReference _controller = Substitute.For<IJSObjectReference>();

    public ShopSwipeCarouselTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var runtime = Substitute.For<IJSRuntime>();
        var module = Substitute.For<IJSObjectReference>();
        runtime.InvokeAsync<IJSObjectReference>("import", Arg.Is<object?[]>(a => (string)a[0]! == "./js/shopSwipeCarousel.js"))
            .Returns(new ValueTask<IJSObjectReference>(module));
        module.InvokeAsync<IJSObjectReference>("mount", Arg.Any<object?[]>())
            .Returns(new ValueTask<IJSObjectReference>(_controller));
        Services.AddSingleton(runtime);
    }

    private IRenderedComponent<ShopSwipeCarousel<string>> Open(int count = 3, int index = 0, bool enabled = true, Action<int>? changed = null) =>
        Render<ShopSwipeCarousel<string>>(p => p
            .Add(c => c.Items, Enumerable.Range(0, count).Select(i => $"https://images.example/{i}.png").ToArray())
            .Add(c => c.ImageSource, value => value)
            .Add(c => c.ImageAlt, _ => "Photo of Peach Ice")
            .Add(c => c.PlaceholderLabel, "Peach Ice")
            .Add(c => c.Label, "Product images")
            .Add(c => c.PositionLabel, (position, total) => $"Image {position} of {total}")
            .Add(c => c.ActiveIndex, index)
            .Add(c => c.Enabled, enabled)
            .Add(c => c.ActiveIndexChanged, value => changed?.Invoke(value)));

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public void Render_MultipleImages_UsesSharedImagesAndPerceivablePosition()
    {
        var cut = Open(index: 1);
        cut.FindComponents<ShopImage>().Should().HaveCount(3);
        cut.FindComponents<ShopImage>().Should().OnlyContain(i => i.Instance.PlaceholderLabel == "Peach Ice" && i.Instance.Preset == ShopImagePreset.SquareContain);
        cut.Find("[role='region']").GetAttribute("aria-label").Should().Be("Product images");
        cut.Find("[data-active-image]").GetAttribute("data-active-image").Should().Be("2");
        cut.Find("[aria-live='polite']").TextContent.Should().Be("Image 2 of 3");
        cut.Find(".shop-swipe-carousel-viewport").GetAttribute("tabindex").Should().Be("0");
        cut.FindAll("img").Select(i => i.GetAttribute("src")).Should().Equal(
            "https://images.example/0.png", "https://images.example/1.png", "https://images.example/2.png");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(3, false)]
    [Trait("Feature", "001_product-detail")]
    public void Render_NoNavigation_OmitsIndicatorAndKeyboardStop(int count, bool enabled)
    {
        var cut = Open(count, enabled: enabled);
        cut.FindAll("[aria-live], .shop-swipe-carousel-indicator").Should().BeEmpty();
        cut.Find(".shop-swipe-carousel-viewport").HasAttribute("tabindex").Should().BeFalse();
        cut.FindComponents<ShopImage>().Should().HaveCount(count);
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(8, 2)]
    [Trait("Feature", "001_product-detail")]
    public void Render_OutOfRangeIndex_ClampsAndReportsBoundary(int index, int expected)
    {
        var reported = new List<int>();
        var cut = Open(index: index, changed: reported.Add);
        cut.Find("[data-active-image]").GetAttribute("data-active-image").Should().Be((expected + 1).ToString());
        reported.Should().Equal(expected);
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Navigate_ValidSettledIndex_ReportsOnlyIndexWithoutMutatingItems()
    {
        var reported = new List<int>();
        var cut = Open(changed: reported.Add);
        var items = cut.Instance.Items;
        await cut.InvokeAsync(() => cut.Instance.OnCarouselIndexChanged(2));
        reported.Should().Equal(2);
        cut.Instance.Items.Should().BeSameAs(items);
        cut.Instance.ActiveIndex.Should().Be(0);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(3, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [Trait("Feature", "001_product-detail")]
    public async Task Navigate_InvalidUnchangedOrStaticIndex_IgnoresCallback(int index, bool enabled)
    {
        var reported = new List<int>();
        var cut = Open(enabled: enabled, changed: reported.Add);
        await cut.InvokeAsync(() => cut.Instance.OnCarouselIndexChanged(index));
        reported.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "001_product-detail")]
    public async Task Dispose_LateBrowserCallback_DoesNotNotifyCaller()
    {
        var reported = new List<int>();
        var cut = Open(changed: reported.Add);
        await cut.Instance.DisposeAsync();
        await cut.Instance.OnCarouselIndexChanged(1);
        reported.Should().BeEmpty();
        _controller.ReceivedCalls().Should().Contain(c => c.GetMethodInfo().Name == "DisposeAsync");
    }
}
