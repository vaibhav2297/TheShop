using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class ShopTableTests : TestContext
{
    private static readonly Row[] Rows = [new(1, "First"), new(2, "Second")];

    public ShopTableTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<ShopTable<Row>> Create(Action<ComponentParameterCollectionBuilder<ShopTable<Row>>>? configure = null)
        => Render<ShopTable<Row>>(p =>
        {
            p.Add(c => c.Items, Rows).Add(c => c.ItemKey, row => row.Id)
                .Add(c => c.Caption, Strings.ManageBrands_Heading).Add(c => c.ColumnCount, 1)
                .Add(c => c.HeaderContent, "<th scope='col'>Name</th>")
                .Add(c => c.RowTemplate, row => builder =>
                {
                    builder.OpenElement(0, "td");
                    builder.AddContent(1, row.Name);
                    builder.CloseElement();
                });
            configure?.Invoke(p);
        });

    private static void Selection(ComponentParameterCollectionBuilder<ShopTable<Row>> p) =>
        p.Add(c => c.Selectable, true).Add(c => c.RowSelectionLabel, row => $"Select {row.Name}");

    [Fact]
    public void Render_Defaults_UsesSemanticTableAndForwardsAttributesToTable()
    {
        var cut = Create(p => p.Add(c => c.Class, "custom").Add(c => c.Style, "--test:1")
            .AddUnmatched("data-testid", "table"));
        cut.Find("table").ClassList.Should().Contain("custom");
        cut.Find("table").GetAttribute("data-testid").Should().Be("table");
        cut.Find("table").GetAttribute("style").Should().Be("--test:1");
        cut.Find("caption").TextContent.Should().Be(Strings.ManageBrands_Heading);
        cut.Find("[role=region]").GetAttribute("tabindex").Should().Be("0");
        cut.Find("thead th").GetAttribute("scope").Should().Be("col");
        cut.FindAll("tbody tr").Should().HaveCount(2);
        cut.FindAll("input").Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");
    }

    [Theory]
    [InlineData(false, "1")]
    [InlineData(true, "2")]
    public void Render_Empty_SpansDataAndSelectionColumns(bool selectable, string columns)
    {
        var cut = Create();
        cut.Render(p => p.Add(c => c.Selectable, selectable)
            .Add(c => c.RowSelectionLabel, row => $"Select {row.Name}")
            .Add(c => c.Items, []).Add(c => c.EmptyContent, "Nothing here"));
        cut.Find("tbody td").GetAttribute("colspan").Should().Be(columns);
        cut.Find("tbody td").TextContent.Should().Be("Nothing here");
        if (selectable) cut.Find("input").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void Selection_OneRow_EmitsFreshKeysWithoutMutatingParent()
    {
        var original = new HashSet<object> { 99 };
        IReadOnlySet<object>? received = null;
        var cut = Create(p =>
        {
            Selection(p);
            p.Add(c => c.SelectedKeys, original).Add(c => c.SelectedKeysChanged, value => received = value);
        });
        cut.Find("tbody input").Change(true);
        received.Should().BeEquivalentTo(new object[] { 99, 1 });
        original.Should().Equal(99);
        cut.Render(p => p.Add(c => c.SelectedKeys, received!));
        cut.Find("thead input").GetAttribute("aria-checked").Should().Be("mixed");
        cut.Find("tbody input").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void Selection_SelectAll_AffectsOnlyDisplayedKeysAndShowsAllState()
    {
        IReadOnlySet<object> selected = new HashSet<object> { 99, 1 };
        var cut = Create(p =>
        {
            Selection(p);
            p.Add(c => c.SelectedKeys, selected).Add(c => c.SelectedKeysChanged, value => selected = value);
        });
        cut.Find("thead input").Change(true);
        selected.Should().BeEquivalentTo(new object[] { 99, 1, 2 });
        cut.Render(p => p.Add(c => c.SelectedKeys, selected));
        cut.Find("thead input").HasAttribute("checked").Should().BeTrue();
        cut.Find("thead input").HasAttribute("aria-checked").Should().BeFalse();
        cut.Find("thead input").Change(false);
        selected.Should().Equal(99);
    }

    [Fact]
    public void Selection_RefreshedAndReorderedDtos_PreservesIdentityAndDoesNotEmit()
    {
        var calls = 0;
        var cut = Create(p =>
        {
            Selection(p);
            p.Add(c => c.SelectedKeys, new HashSet<object> { 1 })
                .Add(c => c.SelectedKeysChanged, _ => calls++);
        });
        var id = cut.Find("tbody input").Id;
        cut.Render(p => p.Add(c => c.Items, [new(2, "Second updated"), new(1, "First updated")]));
        var inputs = cut.FindAll("tbody input");
        inputs[1].Id.Should().Be(id);
        inputs[1].HasAttribute("checked").Should().BeTrue();
        inputs[0].HasAttribute("checked").Should().BeFalse();
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Selection_Disabled_RejectsNativeAndSyntheticChanges()
    {
        var calls = 0;
        var cut = Create(p =>
        {
            Selection(p);
            p.Add(c => c.SelectionDisabled, true).Add(c => c.SelectedKeysChanged, _ => calls++);
        });
        cut.FindAll("input").Should().OnlyContain(input => input.HasAttribute("disabled"));
        cut.Find("tbody input").Change(true);
        foreach (var checkbox in cut.FindComponents<ShopCheckbox>())
            await cut.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(true));
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Selection_RemovedRow_RejectsStaleCallback()
    {
        var calls = 0;
        var cut = Create(p => { Selection(p); p.Add(c => c.SelectedKeysChanged, _ => calls++); });
        var callback = cut.FindComponents<ShopCheckbox>()[1].Instance.ValueChanged;
        cut.Render(p => p.Add(c => c.Items, [Rows[1]]));
        await cut.InvokeAsync(() => callback.InvokeAsync(true));
        calls.Should().Be(0);
    }

    [Fact]
    public void Render_DuplicateKeys_FailsClearly()
    {
        var cut = Create();
        var action = () => cut.Render(p => p.Add(c => c.Items, [Rows[0], Rows[0]]));
        action.Should().Throw<ArgumentException>().WithMessage("*unique*");
    }

    [Fact]
    public void Render_NullKey_FailsClearly()
    {
        var cut = Create();
        var action = () => cut.Render(p => p.Add(c => c.ItemKey, _ => null!));
        action.Should().Throw<ArgumentException>().WithMessage("*non-null*");
    }

    [Fact]
    public void Render_BlankCaption_FailsClearly()
    {
        var cut = Create();
        var action = () => cut.Render(p => p.Add(c => c.Caption, " "));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Render_InvalidColumnCount_FailsClearly()
    {
        var cut = Create();
        var action = () => cut.Render(p => p.Add(c => c.ColumnCount, 0));
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed record Row(int Id, string Name);
}
