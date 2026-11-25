using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using TheShop.Web.Components.Common;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Components.Common;

public class OtpInputTests : TestContext
{
    public OtpInputTests()
    {
        var module = JSInterop.SetupModule("./js/shopOtpInput.js");
        module.SetupVoid("registerPaste", _ => true).SetVoidResult();
        module.SetupVoid("focusInput", _ => true).SetVoidResult();
    }

    [Fact]
    public void Render_Defaults_RendersLabellessNativeDigitFieldsAndForwardsRoot()
    {
        var cut = Render<OtpInput>(p => p
            .Add(c => c.Class, "custom-otp")
            .Add(c => c.Style, "--shop-test: 1;")
            .AddUnmatched("data-testid", "otp-input"));

        var root = cut.Find("[data-testid='otp-input']");
        root.ClassList.Should().Contain("shop-otp-input").And.Contain("custom-otp");
        root.GetAttribute("style").Should().Be("--shop-test: 1;");
        root.Id.Should().StartWith("otp-");
        cut.FindAll("label, .shop-otp-input-hint").Should().BeEmpty();
        cut.Markup.Should().NotContain("mud-");

        var inputs = cut.FindAll("input.shop-otp-digit");
        inputs.Should().HaveCount(6);
        for (var i = 0; i < inputs.Count; i++)
        {
            inputs[i].ClassList.Should().Contain("shop-field-input");
            inputs[i].GetAttribute("aria-label").Should().Be(string.Format(Strings.OtpInput_DigitAriaLabel, i + 1, 6));
            inputs[i].GetAttribute("inputmode").Should().Be("numeric");
            inputs[i].GetAttribute("autocomplete").Should().Be("one-time-code");
            inputs[i].HasAttribute("aria-describedby").Should().BeFalse();
        }
    }

    [Fact]
    public void Input_DigitsRejectedCharactersAndAutofill_EmitSanitizedValueAndCompleteOnce()
    {
        var values = new List<string>();
        var completed = new List<string>();
        var cut = Render<OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.ValueChanged, values.Add)
            .Add(c => c.OnComplete, completed.Add));

        cut.FindAll("input")[0].Input("7");
        cut.FindAll("input")[1].Input("a");

        values.Should().Equal("7");
        cut.FindAll("input")[1].GetAttribute("value").Should().BeNullOrEmpty();

        cut.FindAll("input")[0].Input("12a34");

        values.Should().Equal("7", "1234");
        completed.Should().Equal("1234");
        cut.FindAll("input").Select(i => i.GetAttribute("value") ?? string.Empty).Should().Equal("1", "2", "3", "4");

        cut.FindAll("input")[3].Input("");
        cut.FindAll("input")[3].Input("4");

        values.Should().Equal("7", "1234", "123", "1234");
        completed.Should().Equal("1234", "1234");
    }

    [Fact]
    public void KeyDown_BackspaceOnEmptyBox_ClearsPreviousDigit()
    {
        var value = string.Empty;
        var cut = Render<OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "12")
            .Add(c => c.ValueChanged, v => value = v));

        cut.FindAll("input")[2].KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        value.Should().Be("1");
        cut.FindAll("input")[1].GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ValueAndPaste_DistributeOnlyDigitsAcrossBoxes()
    {
        var value = string.Empty;
        var cut = Render<OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Value, "9x8")
            .Add(c => c.ValueChanged, v => value = v));

        cut.FindAll("input").Select(i => i.GetAttribute("value") ?? string.Empty).Should().Equal("9", "8", "", "");

        await cut.InvokeAsync(() => cut.Instance.HandlePasteAsync("12-34-56"));

        value.Should().Be("1234");
        cut.FindAll("input").Select(i => i.GetAttribute("value") ?? string.Empty).Should().Equal("1", "2", "3", "4");
    }

    [Fact]
    public async Task Disabled_DisablesEveryBoxAndIgnoresPasteAndDigitKeys()
    {
        var calls = 0;
        var cut = Render<OtpInput>(p => p
            .Add(c => c.Length, 4)
            .Add(c => c.Disabled, true)
            .Add(c => c.HelperText, Strings.Verify_CodeLabel)
            .Add(c => c.ValueChanged, _ => calls++));

        cut.FindAll("input").Should().OnlyContain(i => i.HasAttribute("disabled"));
        await cut.InvokeAsync(() => cut.Instance.HandlePasteAsync("1234"));
        await cut.InvokeAsync(() => cut.Instance.HandleDigitKeyAsync(0, "1"));
        calls.Should().Be(0);

        var hint = cut.Find(".shop-otp-input-hint");
        hint.TextContent.Should().Be(Strings.Verify_CodeLabel);
        cut.FindAll("input").Should().OnlyContain(i => i.GetAttribute("aria-describedby") == hint.Id);
    }
}
