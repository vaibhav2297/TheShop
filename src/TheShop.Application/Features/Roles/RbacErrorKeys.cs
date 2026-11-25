namespace TheShop.Application.Features.Roles;

/// <summary>
/// String constants matching the resx keys in <c>Strings.resx</c> for RBAC-related errors.
/// Kept here because the Application layer cannot reference the Web project's typed
/// <c>Strings</c> accessor. Keys are surfaced to the UI verbatim and resolved via
/// <c>Localizer[result.Error]</c>.
/// </summary>
public static class RbacErrorKeys
{
    public const string AccessDenied = "Rbac_AccessDenied";
}
