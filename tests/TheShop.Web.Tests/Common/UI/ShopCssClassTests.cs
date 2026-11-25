using FluentAssertions;
using TheShop.Web.Common.UI;
using Xunit;

namespace TheShop.Web.Tests.Common.UI;

public class ShopCssClassTests
{
    [Fact]
    public void Join_BlankAndPaddedGroups_PreservesNonblankClassGroupsInOrder()
    {
        ShopCssClass.Join(" shop-button ", null, "", "  ", "shop-button-error shop-action")
            .Should().Be("shop-button shop-button-error shop-action");
        ShopCssClass.Join().Should().BeEmpty();
    }

    [Fact]
    public void Modifier_SharedEnums_UsesTheRequestedComponentPrefix()
    {
        ShopCssClass.Modifier("shop-button", ShopColor.Error).Should().Be("shop-button-error");
        ShopCssClass.Modifier("shop-badge", ShopColor.Success).Should().Be("shop-badge-success");
        ShopCssClass.Modifier("shop-button", ShopVariant.Outlined).Should().Be("shop-button-outlined");
        ShopCssClass.Modifier("shop-icon", ShopSize.Small).Should().Be("shop-icon-small");
    }

    [Theory]
    [InlineData(SampleOption.ExtraSmall, "extra-small")]
    [InlineData(SampleOption.HTTPStatus, "http-status")]
    public void Modifier_MultiwordEnum_ProducesKebabCase(SampleOption value, string suffix)
    {
        ShopCssClass.Modifier("shop-sample", value).Should().Be("shop-sample-" + suffix);
    }

    [Fact]
    public void Modifier_UndefinedEnum_RejectsNumericClassesAndIdentifiesTheCaller()
    {
        var Color = (ShopColor)999;
        Action build = () => ShopCssClass.Modifier("shop-button", Color);
        build.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(nameof(Color));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("shop-button shop-other")]
    [InlineData("shop-button\nother")]
    public void Modifier_InvalidPrefix_RejectsAClassList(string? prefix)
    {
        Action build = () => ShopCssClass.Modifier(prefix!, ShopColor.Primary);
        build.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("prefix");
    }

    public enum SampleOption { ExtraSmall, HTTPStatus }
}
