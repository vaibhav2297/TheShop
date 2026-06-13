using System.Reflection;
using Bunit;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Auth.Commands.RequestSignInOtp;
using TheShop.Application.Features.Auth.DTOs;
using TheShop.Web.Common;
using TheShop.Web.Pages.Auth;
using TheShop.Web.Resources;
using Xunit;

namespace TheShop.Web.Tests.Pages.Auth;

/// <summary>
/// Tests for the <see cref="SignIn"/> page (sign-in step 1: email entry).
/// Covers FR-2, FR-3, AC-2, AC-5 at the UI layer.
/// <see href=".specs/authentication/spec.md"/>
/// </summary>
public class SignInTests : TestContext
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly IStringLocalizer<Strings> _localizer = Substitute.For<IStringLocalizer<Strings>>();

    public SignInTests()
    {
        // BusyState is a concrete class with no constructor params — use the real one.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupVoid(i => true).SetVoidResult();
        Services.AddSingleton<BusyState>();
        Services.AddSingleton(_mediator);
        Services.AddSingleton(_snackbar);
        Services.AddSingleton(_localizer);
        Services.AddSingleton(Substitute.For<TheShop.Web.Theme.ShopTheme>());
        Services.AddMudServices();
        Services.Replace(ServiceDescriptor.Singleton(Substitute.For<IPopoverService>()));

        // Localize any error key to itself so assertions stay key-based.
        _localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key);
        });
    }

    // =========================================================================
    // Happy path — successful send (AC-2, FR-2)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenMediatorReturnsSuccess_ShowsSuccessSnackbarAndNavigates()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Ok(new OtpRequestedDto("user@example.com", 60)));

        var cut = Render<SignIn>();
        var navManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        // MudForm has a validation delay that cannot be driven in bUnit without a real timer.
        // Set the private backing fields directly so the button is enabled when clicked.
        SetSignInState(cut, email: "user@example.com", isFormValid: true);

        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _snackbar.Received().Add(
            Arg.Is<string>(s => s == Strings.Auth_CodeSent),
            Severity.Success);
        navManager.Uri.Should().Contain(Routes.Auth.SignInVerify);
    }

    // =========================================================================
    // Account not found (AC-5)
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public async Task OnSendCodeAsync_WhenAccountNotFound_ShowsErrorSnackbar()
    {
        _mediator.Send(Arg.Any<RequestSignInOtpCommand>(), Arg.Any<CancellationToken>())
                 .Returns(Result.Fail<OtpRequestedDto>("Auth_AccountNotFound"));

        var cut = Render<SignIn>();

        SetSignInState(cut, email: "ghost@example.com", isFormValid: true);

        var button = cut.Find("button[type='button']");
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _snackbar.Received().Add(
            Arg.Any<string>(),
            Severity.Error);
    }

    private static void SetSignInState(IRenderedComponent<SignIn> cut, string email, bool isFormValid)
    {
        var type = typeof(SignIn);
        type.GetField("_email", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, email);
        type.GetField("_isFormValid", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(cut.Instance, isFormValid);
        cut.Render();
    }

    // =========================================================================
    // Render — page loads correctly
    // =========================================================================

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsEmailInput()
    {
        var cut = Render<SignIn>();
        cut.Find("input[type='email']").Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "authentication")]
    public void Render_Always_ContainsLinkToSignUpPage()
    {
        var cut = Render<SignIn>();
        var link = cut.Find($"a[href='{Routes.Auth.SignUp}']");
        link.Should().NotBeNull();
    }
}
