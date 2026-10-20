using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Common.UI;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopBulkActionBarTests : TestContext
{
    private readonly BunitJSModuleInterop _module;

    public ShopBulkActionBarTests()
    {
        _module = JSInterop.SetupModule("./js/shopBulkActionBar.js");
        _module.SetupVoid("sync", _ => true).SetVoidResult();
        _module.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    [Fact]
    public void Render_Hidden_DoesNotRenderOrImportJavaScript()
    {
        var cut = Render<ShopBulkActionBar>(p => p.Add(c => c.Visible, false));
        cut.Markup.Should().BeEmpty();
        JSInterop.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, "01")]
    [InlineData(12, "12")]
    [InlineData(123, "123")]
    public void Render_Visible_UsesFigmaCountAndAccessibleStatus(int count, string display)
    {
        var cut = Render<ShopBulkActionBar>(p => p.Add(c => c.Visible, true).Add(c => c.SelectedCount, count));
        cut.Find(".shop-bulk-action-count").TextContent.Should().Be(display);
        cut.Find("[role='status'] .shop-visually-hidden").TextContent.Should().Be(string.Format(Strings.BulkActions_SelectedCount, count));
        cut.Find("section").GetAttribute("aria-label").Should().Be(Strings.BulkActions_Label);
        cut.Markup.Should().NotContain("mud-");
        var close = cut.FindComponent<ShopIconButton>().Instance;
        close.Variant.Should().Be(ShopVariant.Outlined);
        close.Size.Should().Be(ShopSize.Medium);
        close.Label.Should().Be(Strings.Close_BulkActionBar);
    }

    [Fact]
    public void Render_AttributesAndActions_TargetTheBarNotThePositioningSlot()
    {
        var cut = Render<ShopBulkActionBar>(p => p.Add(c => c.Visible, true)
            .Add(c => c.Class, "custom").Add(c => c.Style, "opacity:0.5")
            .AddUnmatched("data-testid", "bulk").AddUnmatched("aria-label", "wrong")
            .Add(c => c.Actions, (RenderFragment)(b => b.AddMarkupContent(0, "<button id='action'>Action</button>"))));
        var bar = cut.Find("section");
        bar.ClassList.Should().Contain("custom");
        bar.GetAttribute("style").Should().Be("opacity:0.5");
        bar.GetAttribute("data-testid").Should().Be("bulk");
        bar.GetAttribute("aria-label").Should().Be(Strings.BulkActions_Label);
        cut.Find(".shop-bulk-action-actions #action").Should().NotBeNull();
        cut.Find(".shop-bulk-action-slot").ClassList.Should().NotContain("custom");
    }

    [Fact]
    public void Close_Activated_RequestsPageClearWithoutOwningVisibility()
    {
        var called = 0;
        var cut = Render<ShopBulkActionBar>(p => p.Add(c => c.Visible, true).Add(c => c.OnClose, () => called++));
        cut.Find("button").Click();
        called.Should().Be(1);
        cut.Find("section").Should().NotBeNull();
    }

    [Fact]
    public async Task Lifecycle_ShowHideShowAndDispose_SynchronizesOneStableRegistration()
    {
        var cut = Render<ShopBulkActionBar>();
        cut.Render(p => p.Add(c => c.Visible, true));
        var id = cut.Find(".shop-bulk-action-slot").Id;
        cut.Render(p => p.Add(c => c.SelectedCount, 2));
        cut.Render(p => p.Add(c => c.Visible, false));
        cut.Markup.Should().BeEmpty();
        cut.Render(p => p.Add(c => c.Visible, true));
        cut.Find(".shop-bulk-action-slot").Id.Should().Be(id);
        _module.Invocations["sync"].Should().HaveCount(4);
        await cut.Instance.DisposeAsync();
        await cut.Instance.DisposeAsync();
        _module.Invocations["dispose"].Should().ContainSingle();
        _module.Invocations["dispose"].Single().Arguments[0].Should().Be(id);
    }
}
