using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TheShop.Application.Features.Auth;
using TheShop.Application.Features.Auth.Commands.VerifySignInOtp;
using Xunit;

namespace TheShop.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ValidationDefaults_StopWithinRulesAndContinueAcrossFields()
    {
        var services = new ServiceCollection().AddApplication();
        var validator = new VerifySignInOtpCommandValidator();

        var result = validator.Validate(new VerifySignInOtpCommand("", ""));

        ValidatorOptions.Global.DefaultRuleLevelCascadeMode.Should().Be(CascadeMode.Stop);
        ValidatorOptions.Global.DefaultClassLevelCascadeMode.Should().Be(CascadeMode.Continue);
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IValidator<VerifySignInOtpCommand>)
            && descriptor.ImplementationType == typeof(VerifySignInOtpCommandValidator));
        result.Errors.Select(error => error.PropertyName).Should().Equal(
            nameof(VerifySignInOtpCommand.Email), nameof(VerifySignInOtpCommand.Code));
        result.Errors.Select(error => error.ErrorMessage).Should().Equal(
            AuthErrorKeys.EmailRequired, AuthErrorKeys.CodeInvalidOrExpired);
    }
}
