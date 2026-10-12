using TheShop.Web.Common.Notifications;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;


using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.VerifySignUpOtp;
using TheShop.Application.Features.Auth.Commands.ResendOtp;
using TheShop.Domain.Enums;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Pages.Auth;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Auth;

/// <summary>
/// Tests for the <see cref="SignUpVerify"/> page (sign-up step 2: code entry).
/// Covers FR-4, FR-5, AC-1, AC-3, AC-6, AC-7 at the UI layer.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignUpVerifyTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public SignUpVerifyTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);




        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    [Fact]
    public async Task Submit_LeadingZero_PreservesProfileClearsPendingStateAndNavigatesHome()
    {
        var state = PopulatedState();
        Services.AddSingleton(state);
        _mediator.Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Ok<SessionDto>(null!));
        var cut = Render<SignUpVerify>();
        cut.Markup.Should().NotContain("mud-");
        cut.Find("[data-testid='otp-change']").GetAttribute("href").Should().Be(Routes.Auth.SignUp);
        cut.Find("[data-testid='otp-countdown']").TextContent.Should().Be("01:00");
        cut.Find("input").Input("012345");
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(new VerifySignUpOtpCommand("new@example.com", "012345", "Jane", "Doe", new DateOnly(2000, 1, 1)), Arg.Any<CancellationToken>());
        state.HasData.Should().BeFalse();
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Home);
    }

    [Fact]
    public async Task Submit_InFlight_BlocksDuplicateAndClearsFailedCodeWithoutLosingProfile()
    {
        var state = PopulatedState();
        Services.AddSingleton(state);
        var completion = new TaskCompletionSource<Result<SessionDto>>();
        _mediator.Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var cut = Render<SignUpVerify>();
        cut.Find("input").Input("012345");
        var submit = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => cut.FindAll("input").Should().OnlyContain(input => input.HasAttribute("disabled")));
        await cut.Find("form").SubmitAsync();
        await _mediator.Received(1).Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>());
        completion.SetResult(Result.Fail<SessionDto>(nameof(Strings.Auth_Code_InvalidOrExpired)));
        await submit;
        state.HasData.Should().BeTrue();
        cut.FindAll("input").Should().OnlyContain(input => string.IsNullOrEmpty(input.GetAttribute("value")));
        cut.Find("[data-testid='otp-submit']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Submit_IncompleteCode_ShowsCommandErrorAndClearsOnEdit()
    {
        Services.AddSingleton(PopulatedState());
        _mediator.Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<SessionDto>.Fail(nameof(Strings.Auth_Code_InvalidOrExpired), [],
                [new("Code", nameof(Strings.Auth_Code_InvalidOrExpired))]));
        var cut = Render<SignUpVerify>();
        cut.Find("input").Input("012");
        await cut.Find("form").SubmitAsync();
        cut.Find("#signup-code-error").TextContent.Should().Contain(nameof(Strings.Auth_Code_InvalidOrExpired));
        cut.Find("input").Input("0123");
        cut.Find("#signup-code-error").TextContent.Should().BeEmpty();
        await cut.Find("form").SubmitAsync();
        cut.Find("#signup-code-error").TextContent.Should().Contain(nameof(Strings.Auth_Code_InvalidOrExpired));
    }

    [Fact]
    public async Task Resend_AfterCooldown_PreservesPurposeAndBlocksOverlappingRequests()
    {
        Services.AddSingleton(PopulatedState());
        var completion = new TaskCompletionSource<Result<OtpRequestedDto>>();
        _mediator.Send(Arg.Any<ResendOtpCommand>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var cut = Render<SignUpVerify>();
        // Advance only the timer state; all request dispatch goes through rendered controls.
        typeof(SignUpVerify).GetField("_resendCooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(cut.Instance, 0);
        cut.Render();
        cut.Find("input").Input("012345");
        var resend = cut.Find("[data-testid='otp-resend']").ClickAsync(new());
        cut.WaitForAssertion(() => cut.FindAll("input").Should().OnlyContain(input => input.HasAttribute("disabled")));
        await cut.Find("form").SubmitAsync();
        await _mediator.DidNotReceive().Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(new ResendOtpCommand("new@example.com", OtpPurpose.SignUp), Arg.Any<CancellationToken>());
        completion.SetResult(Result.Ok(new OtpRequestedDto("new@example.com", 45)));
        await resend;
        cut.Find("[data-testid='otp-countdown']").TextContent.Should().Be("00:45");
        cut.FindAll("input").Should().OnlyContain(input => !input.HasAttribute("disabled"));
    }

    private PendingSignUpState PopulatedState()
    {
        var state = new PendingSignUpState();
        state.Set("Jane", "Doe", "new@example.com", new DateOnly(2000, 1, 1));
        return state;
    }

    // =========================================================================
    // Guard: no pending data → redirect to sign-up
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void OnInitialized_WhenNoPendingSignUpData_RedirectsToSignUp()
    {
        // Arrange: register an empty PendingSignUpState (HasData = false)
        Services.AddSingleton(new PendingSignUpState());

        var cut = Render<SignUpVerify>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        navManager.Uri.Should().Contain(Routes.Auth.SignUp);
    }

    // =========================================================================
    // Guard: pending data present → stays on verify page
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void OnInitialized_WhenPendingSignUpDataPresent_DoesNotRedirect()
    {
        Services.AddSingleton(PopulatedState());

        var cut = Render<SignUpVerify>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        navManager.Uri.Should().NotContain(Routes.Auth.SignUp);
    }

    // =========================================================================
    // Too many attempts → redirect + clear pending state (AC-7)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnVerifyAsync_WhenTooManyAttempts_ClearsPendingStateAndRedirectsToSignUp()
    {
        var state = PopulatedState();
        Services.AddSingleton(state);

        _mediator.Send(Arg.Any<VerifySignUpOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<SessionDto>("Auth_TooManyAttempts"));

        var cut = Render<SignUpVerify>();

        cut.Find("input").Input("012345");
        await cut.Find("form").SubmitAsync();
        state.HasData.Should().BeFalse();
        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith(Routes.Auth.SignUp);
        _notifications.Received().Show(nameof(Strings.Auth_TooManyAttempts), ShopNotificationKind.Error);
    }

    // =========================================================================
    // PendingSignUpState: Set and HasData
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_AfterSet_HasData()
    {
        var state = new PendingSignUpState();
        state.Set("A", "B", "a@b.com", new DateOnly(2000, 6, 15));

        state.HasData.Should().BeTrue();
        state.FirstName.Should().Be("A");
        state.LastName.Should().Be("B");
        state.Email.Should().Be("a@b.com");
        state.DateOfBirth.Should().Be(new DateOnly(2000, 6, 15));
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_AfterClear_HasNoData()
    {
        var state = PopulatedState();
        state.Clear();

        state.HasData.Should().BeFalse();
        state.FirstName.Should().BeNull();
        state.LastName.Should().BeNull();
        state.Email.Should().BeNull();
        state.DateOfBirth.Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(PendingSignUpState.FirstName), null)]
    [InlineData(nameof(PendingSignUpState.FirstName), "")]
    [InlineData(nameof(PendingSignUpState.LastName), null)]
    [InlineData(nameof(PendingSignUpState.LastName), "")]
    [InlineData(nameof(PendingSignUpState.Email), null)]
    [InlineData(nameof(PendingSignUpState.Email), "")]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_WhenRequiredStringFieldIsNullOrEmpty_HasDataReturnsFalse(
        string propertyName, string? value)
    {
        var state = new PendingSignUpState();
        state.Set("Jane", "Doe", "a@b.com", new DateOnly(2000, 1, 1));

        // PendingSignUpState.Set() is all-or-nothing; use reflection to create a partial
        // state and verify that HasData correctly returns false for each missing field.
        typeof(PendingSignUpState).GetProperty(propertyName)!.SetValue(state, value);

        state.HasData.Should().BeFalse(
            $"{propertyName} = '{value ?? "null"}' must cause HasData to return false");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_WhenDateOfBirthIsNull_HasDataReturnsFalse()
    {
        var state = new PendingSignUpState();
        state.Set("Jane", "Doe", "a@b.com", new DateOnly(2000, 1, 1));

        typeof(PendingSignUpState)
            .GetProperty(nameof(PendingSignUpState.DateOfBirth))!
            .SetValue(state, null);

        state.HasData.Should().BeFalse("null DateOfBirth must cause HasData to return false");
    }
}
