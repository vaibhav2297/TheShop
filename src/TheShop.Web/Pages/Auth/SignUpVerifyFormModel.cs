namespace TheShop.Web.Pages.Auth;

/// <summary>Signup verification code editing state; Application owns code validation.</summary>
public sealed class SignUpVerifyFormModel
{
    /// <summary>Code text preserves leading zeroes.</summary>
    public string Code { get; set; } = string.Empty;
}
