namespace TheShop.Web.State;

/// <summary>
/// Scoped store that carries sign-up profile data between the "Send code" step
/// and the "Verify code" step. Tab-lifetime in WASM. See plan §5 decision #2.
/// </summary>
public sealed class PendingSignUpState
{
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Email { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }

    public bool HasData =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        !string.IsNullOrWhiteSpace(Email) &&
        DateOfBirth.HasValue;

    public void Set(string firstName, string lastName, string email, DateOnly dateOfBirth)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        DateOfBirth = dateOfBirth;
    }

    public void Clear()
    {
        FirstName = null;
        LastName = null;
        Email = null;
        DateOfBirth = null;
    }
}
