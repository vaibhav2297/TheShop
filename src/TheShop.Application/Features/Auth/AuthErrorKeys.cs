namespace TheShop.Application.Features.Auth;

/// <summary>
/// String constants matching the resx keys in <c>Strings.resx</c> for auth-related errors.
/// Kept here because the Application layer cannot reference the Web project's typed
/// <c>Strings</c> accessor. Keys are surfaced to the UI verbatim and resolved via
/// <c>Localizer[result.Error]</c>.
/// </summary>
public static class AuthErrorKeys
{
    public const string AccountAlreadyExists = "Auth_AccountAlreadyExists";
    public const string AccountNotFound = "Auth_AccountNotFound";
    public const string Underage = "Auth_Underage";
    public const string CodeIncorrect = "Auth_Code_Incorrect";
    public const string CodeExpired = "Auth_CodeExpired";
    public const string TooManyAttempts = "Auth_TooManyAttempts";
    public const string CodeInvalid = "Auth_Code_Invalid";
    public const string ResendTooSoon = "Auth_ResendTooSoon";
    public const string FirstNameRequired = "Auth_FirstName_Required";
    public const string LastNameRequired = "Auth_LastName_Required";
    public const string DobInPast = "Auth_Dob_InPast";
    public const string EmailRequired = "Email_Required";
    public const string EmailInvalid = "Email_Invalid";
    public const string SessionExpired = "Auth_SessionExpired";
    public const string Network = "Auth_Network";
    public const string Unexpected = "Auth_Unexpected";
}
