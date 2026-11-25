namespace TheShop.Web.Pages.Auth;

/// <summary>Web-only editing state mapped to the existing sign-in command on submit.</summary>
public sealed class SignInFormModel
{
    /// <summary>Email entered by the user; trimmed only when dispatching the command.</summary>
    public string Email { get; set; } = string.Empty;
}
