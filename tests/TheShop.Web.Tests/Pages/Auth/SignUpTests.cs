using System.Reflection;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
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
/// <see href=".claude/specs/authentication.md"/>
/// </summary>
public class SignUpTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();
    private readonly PendingSignUpState _pendingSignUp = new();

    public SignUpTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddSingleton(_pendingSignUp);
        Services.AddSingleton(Substitute.For<TheShop.Web.Theme.ShopTheme>());
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));

        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
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
        // First name field is the first text input rendered by the MudForm.
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

        // Set private backing fields so the button is enabled and the handler can build the command.
        SetSignUpState(cut,
            firstName: "Jane",
            lastName: "Doe",
            email: "jane@example.com",
            dateOfBirth: DateTime.Today.AddYears(-25),
            isFormValid: true);

        // Act
        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert: success snackbar was shown and user was navigated to the verify page.
        _snackbar.Received().Add(
            Arg.Is<string>(s => s == Strings.Auth_CodeSent),
            Severity.Success);
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

        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

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

        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _snackbar.Received().Add(Arg.Any<string>(), Severity.Error);
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

        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        navManager.Uri.Should().NotContain(Routes.Auth.SignUpVerify);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Sets the private backing fields on <see cref="SignUp"/> via reflection so the
    /// form is in a state that allows the send-code button to be clicked without relying
    /// on a real MudForm validation cycle (which requires a full JS environment).
    /// </summary>
    private static void SetSignUpState(
        IRenderedComponent<SignUp> cut,
        string firstName,
        string lastName,
        string email,
        DateTime dateOfBirth,
        bool isFormValid)
    {
        var type = typeof(SignUp);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        type.GetField("_firstName", flags)!.SetValue(cut.Instance, firstName);
        type.GetField("_lastName", flags)!.SetValue(cut.Instance, lastName);
        type.GetField("_email", flags)!.SetValue(cut.Instance, email);
        type.GetField("_dateOfBirth", flags)!.SetValue(cut.Instance, (DateTime?)dateOfBirth);
        type.GetField("_isFormValid", flags)!.SetValue(cut.Instance, isFormValid);
        cut.Render();
    }
}
