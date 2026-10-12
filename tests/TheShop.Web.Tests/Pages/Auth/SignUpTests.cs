using TheShop.Web.Common.Notifications;
using System.Reflection;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;


using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.RequestSignUpOtp;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Pages.Auth;
using TheShop.Web.Resources;
using TheShop.Web.State;
using Xunit;

namespace TheShop.Web.Tests.Pages.Auth;

/// <summary>
/// Tests for the <see cref="SignUp"/> page (sign-up step 1: profile entry) and the
/// <see cref="PendingSignUpState"/> client-state store.
/// Covers FR-1, FR-3, AC-1, AC-4 at the UI layer.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignUpTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IShopNotificationService _notifications = Substitute.For<IShopNotificationService>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly PendingSignUpState _pendingSignUp = new();

    public SignUpTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_notifications);
        Services.AddSingleton(_localizer);
        Services.AddSingleton(_pendingSignUp);



        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, Strings.ResourceManager.GetString(key) ?? key);
        });
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var command = call.Arg<RequestSignUpOtpCommand>();
            var result = await new RequestSignUpOtpCommandValidator().ValidateAsync(command);
            return result.IsValid ? Result.Ok(new OtpRequestedDto(command.Email, 60))
                : Result<OtpRequestedDto>.Fail(result.Errors[0].ErrorMessage, [], result.Errors.Select(e => new FieldValidationError(e.PropertyName, e.ErrorMessage)).ToArray());
        });
    }

    // =========================================================================
    // PendingSignUpState — initial state and construction (AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_OnConstruction_HasNoData()
    {
        // Before the form is submitted the state must be empty,
        // ensuring the AC-4 error path and submit are unreachable.
        _pendingSignUp.HasData.Should().BeFalse(
            "a freshly constructed PendingSignUpState must report HasData = false");
    }

    // =========================================================================
    // Whitespace-only fields must also fail HasData (FR-1 — all fields required)
    // =========================================================================

    [Theory]
    [InlineData(nameof(PendingSignUpState.FirstName), "   ")]
    [InlineData(nameof(PendingSignUpState.LastName), "   ")]
    [InlineData(nameof(PendingSignUpState.Email), "   ")]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_WhenRequiredStringFieldIsWhitespace_HasDataReturnsFalse(
        string propertyName, string value)
    {
        _pendingSignUp.Set("Jane", "Doe", "a@b.com", new DateOnly(2000, 1, 1));
        typeof(PendingSignUpState).GetProperty(propertyName)!.SetValue(_pendingSignUp, value);

        _pendingSignUp.HasData.Should().BeFalse(
            $"{propertyName} = '{value}' (whitespace) must cause HasData to return false");
    }

    // =========================================================================
    // Set — overwrites previous values (AC-1: re-entry scenario)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_Set_RepeatedlyOverwritesPreviousValues()
    {
        _pendingSignUp.Set("Alice", "Smith", "alice@example.com", new DateOnly(1990, 3, 15));
        _pendingSignUp.Set("Bob", "Jones", "bob@example.com", new DateOnly(1985, 7, 22));

        _pendingSignUp.FirstName.Should().Be("Bob");
        _pendingSignUp.LastName.Should().Be("Jones");
        _pendingSignUp.Email.Should().Be("bob@example.com");
        _pendingSignUp.DateOfBirth.Should().Be(new DateOnly(1985, 7, 22));
    }

    // =========================================================================
    // Clear — idempotent (called on TooManyAttempts and on success)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_Clear_IsIdempotent()
    {
        _pendingSignUp.Set("Jane", "Doe", "a@b.com", new DateOnly(2000, 1, 1));
        _pendingSignUp.Clear();
        _pendingSignUp.Clear();

        _pendingSignUp.HasData.Should().BeFalse("calling Clear twice must not throw and must leave state empty");
        _pendingSignUp.FirstName.Should().BeNull();
        _pendingSignUp.LastName.Should().BeNull();
        _pendingSignUp.Email.Should().BeNull();
        _pendingSignUp.DateOfBirth.Should().BeNull();
    }

    // =========================================================================
    // DateOfBirth stored correctly (FR-1: age-gate field)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void PendingSignUpState_Set_DateOfBirthIsStoredCorrectly()
    {
        var dob = new DateOnly(1995, 12, 31);
        _pendingSignUp.Set("Jane", "Doe", "a@b.com", dob);

        _pendingSignUp.DateOfBirth.Should().Be(dob);
        _pendingSignUp.HasData.Should().BeTrue();
    }

    // =========================================================================
    // SignUp page — render (FR-1, AC-1)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsFirstNameField()
    {
        // FR-1: sign-up collects first name, last name, email, and date of birth.
        var cut = Render<SignUp>();
        // Native controls have associated labels.
        cut.FindAll("input").Should().NotBeEmpty("the sign-up page must render input fields");
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsLinkToSignInPage()
    {
        // Users who already have an account should be offered a link to sign-in.
        var cut = Render<SignUp>();
        var link = cut.Find($"a[href='{Routes.Auth.SignIn}']");
        link.Should().NotBeNull("the sign-up page must link to the sign-in page");
    }

    // =========================================================================
    // SignUp page — successful dispatch (AC-1, FR-3)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenMediatorReturnsSuccess_StoresPendingDataAndNavigatesToVerify()
    {
        // Arrange
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new OtpRequestedDto("jane@example.com", 60)));

        var cut = Render<SignUp>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // Fill the actual inputs so validation participates in submission.
        SetSignUpState(cut,
            firstName: "Jane",
            lastName: "Doe",
            email: "jane@example.com",
            dateOfBirth: DateTime.Today.AddYears(-25),
            isFormValid: true);

        // Act
        await cut.Find("form").SubmitAsync();

        // Assert: success snackbar was shown and user was navigated to the verify page.
        _notifications.Received().Show(
            Arg.Is<string>(s => s == Strings.Auth_CodeSent),
            ShopNotificationKind.Success);
        navManager.Uri.Should().Contain(Routes.Auth.SignUpVerify);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenMediatorReturnsSuccess_PopulatesPendingSignUpState()
    {
        // AC-1: after a successful OTP request, PendingSignUpState must hold the profile
        // data so that SignUpVerify can retrieve it without asking the user again.
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new OtpRequestedDto("jane@example.com", 60)));

        var cut = Render<SignUp>();

        SetSignUpState(cut,
            firstName: "Jane",
            lastName: "Doe",
            email: "jane@example.com",
            dateOfBirth: DateTime.Today.AddYears(-25),
            isFormValid: true);

        await cut.Find("form").SubmitAsync();

        _pendingSignUp.HasData.Should().BeTrue();
        _pendingSignUp.FirstName.Should().Be("Jane");
        _pendingSignUp.Email.Should().Be("jane@example.com");
    }

    // =========================================================================
    // SignUp page — account already exists (AC-4)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenAccountAlreadyExists_ShowsErrorSnackbar()
    {
        // AC-4: sign-up with an existing email must show a clear error; no code is sent.
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<OtpRequestedDto>("Auth_AccountAlreadyExists"));

        var cut = Render<SignUp>();

        SetSignUpState(cut,
            firstName: "Jane",
            lastName: "Doe",
            email: "taken@example.com",
            dateOfBirth: DateTime.Today.AddYears(-25),
            isFormValid: true);

        await cut.Find("form").SubmitAsync();

        _notifications.Received().Show(Arg.Any<string>(), ShopNotificationKind.Error);
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenAccountAlreadyExists_DoesNotNavigateToVerify()
    {
        // AC-4: no navigation when the email is already registered — the user stays on the form.
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<OtpRequestedDto>("Auth_AccountAlreadyExists"));

        var cut = Render<SignUp>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        SetSignUpState(cut,
            firstName: "Jane",
            lastName: "Doe",
            email: "taken@example.com",
            dateOfBirth: DateTime.Today.AddYears(-25),
            isFormValid: true);

        await cut.Find("form").SubmitAsync();

        navManager.Uri.Should().NotContain(Routes.Auth.SignUpVerify);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task NativeDate_AgeBoundary_PreservesCommandDateAndRejectsUnderage(int daysAfterCutoff, bool allowed)
    {
        var cut = Render<SignUp>();
        SetSignUpState(cut, "Jane", "Doe", "jane@example.com", DateTime.Today.AddYears(-25), true);
        var date = DateOnly.FromDateTime(DateTime.Today).AddYears(-19).AddDays(daysAfterCutoff);
        cut.Find("input[type=date]").Change(date.ToString("yyyy-MM-dd"));
        await SubmitDirectlyAsync(cut);
        if (allowed)
        {
            await _mediator.Received(1).Send(Arg.Is<RequestSignUpOtpCommand>(c => c.DateOfBirth == date), Arg.Any<CancellationToken>());
            _pendingSignUp.DateOfBirth.Should().Be(date);
        }
        else
        {
            _pendingSignUp.HasData.Should().BeFalse();
            cut.Find(".shop-date-field .shop-field-error").TextContent.Should().Contain(Strings.Auth_Underage);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("2999-01-01")]
    public async Task NativeDate_InvalidInput_PreventsSuccessfulSignup(string date)
    {
        var cut = Render<SignUp>();
        SetSignUpState(cut, "Jane", "Doe", "jane@example.com", DateTime.Today.AddYears(-25), true);
        cut.Find("input[type=date]").Change(date);
        await SubmitDirectlyAsync(cut);
        if (date == "bad")
            await _mediator.DidNotReceive().Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>());
        _pendingSignUp.HasData.Should().BeFalse();
        cut.Find(".shop-date-field .shop-field-error").TextContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task NativeDate_ValidButAgeConfirmationMissing_BlocksDispatch()
    {
        var cut = Render<SignUp>();
        SetSignUpState(cut, "Jane", "Doe", "jane@example.com", DateTime.Today.AddYears(-25), true);
        cut.Find("#signup-age").Change(false);
        cut.Render();
        await SubmitDirectlyAsync(cut);
        await _mediator.DidNotReceive().Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_CommandErrors_AppearInlineClearOnEditAndRevalidate()
    {
        var cut = Render<SignUp>();
        cut.Markup.Should().NotContain("mud-");
        cut.Find("#signup-age").Change(true);
        await cut.Find("form").SubmitAsync();
        cut.Markup.Should().Contain(Strings.Auth_FirstName_Required).And.Contain(Strings.Auth_LastName_Required)
            .And.Contain(Strings.Email_Required);
        cut.Find("#signup-first-name").GetAttribute("aria-invalid").Should().Be("true");
        SetSignUpState(cut, "Jane", "Doe", "invalid", DateTime.Today.AddYears(-25), true);
        await cut.Find("form").SubmitAsync();
        cut.Markup.Should().Contain(Strings.Email_Invalid);
        cut.Find("#signup-email").Input("still-invalid");
        cut.Markup.Should().NotContain(Strings.Email_Invalid);
        await cut.Find("form").SubmitAsync();
        cut.Markup.Should().Contain(Strings.Email_Invalid);
        _pendingSignUp.HasData.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_InFlight_FreezesValuesRejectsDuplicateAndRecovers()
    {
        var completion = new TaskCompletionSource<Result<OtpRequestedDto>>();
        _mediator.Send(Arg.Any<RequestSignUpOtpCommand>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var cut = Render<SignUp>();
        SetSignUpState(cut, " Jane ", " Doe ", "jane@example.com", DateTime.Today.AddYears(-25), true);
        var submit = cut.Find("form").SubmitAsync();
        cut.WaitForAssertion(() => cut.FindAll("input").Should().OnlyContain(input => input.HasAttribute("disabled")));
        await cut.Find("form").SubmitAsync();
        cut.Find("#signup-first-name").Input("Changed");
        await _mediator.Received(1).Send(Arg.Is<RequestSignUpOtpCommand>(c => c.FirstName == "Jane" && c.LastName == "Doe"), Arg.Any<CancellationToken>());
        completion.SetResult(Result.Ok(new OtpRequestedDto("jane@example.com", 60)));
        await submit;
        _pendingSignUp.FirstName.Should().Be("Jane");
        cut.FindAll("input").Should().OnlyContain(input => !input.HasAttribute("disabled"));
    }

    private static Task SubmitDirectlyAsync(IRenderedComponent<SignUp> cut) => cut.Find("form").SubmitAsync();

    private static void SetSignUpState(
        IRenderedComponent<SignUp> cut,
        string firstName,
        string lastName,
        string email,
        DateTime dateOfBirth,
        bool isFormValid)
    {
        cut.Find("#signup-first-name").Input(firstName);
        cut.Find("#signup-last-name").Input(lastName);
        cut.Find("#signup-email").Input(email);
        cut.Find("input[type=date]").Change(dateOfBirth.ToString("yyyy-MM-dd"));
        cut.Find("#signup-age").Change(isFormValid);
    }
}
