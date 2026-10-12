namespace TheShop.Web.Pages.Auth;

/// <summary>Editable signup details and required consent, mapped to the existing signup command.</summary>
public sealed class SignUpFormModel
{
    /// <summary>Given name, trimmed on submission.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Family name, trimmed on submission.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Email receiving the verification code.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Date-only birthday; missing input maps to the command's empty date.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Explicit confirmation of the minimum age.</summary>
    public bool AgeConfirmed { get; set; }
}
