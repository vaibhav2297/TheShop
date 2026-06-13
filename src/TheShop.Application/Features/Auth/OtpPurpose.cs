namespace TheShop.Application.Features.Auth;

/// <summary>
/// Distinguishes the intent of an OTP so <see cref="Commands.ResendOtp.ResendOtpHandler"/>
/// can route the resend to the correct underlying auth flow.
/// </summary>
public enum OtpPurpose
{
    SignUp = 1,
    SignIn = 2,
}
