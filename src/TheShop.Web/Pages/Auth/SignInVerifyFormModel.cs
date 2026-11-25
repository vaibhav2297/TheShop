namespace TheShop.Web.Pages.Auth;

/// <summary>Editable code retained by the sign-in verification form.</summary>
public sealed class SignInVerifyFormModel
{
    /// <summary>Six-digit code, including any leading zeroes.</summary>
    public string Code { get; set; } = string.Empty;
}
