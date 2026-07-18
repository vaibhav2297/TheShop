using System.Text.RegularExpressions;
using TheShop.Domain.Exceptions;

namespace TheShop.Domain.ValueObjects;

/// <summary>
/// A permission code in <c>module.action</c> form (e.g. <c>products.view</c>,
/// <c>orders.refund</c>). Equality is by code.
/// </summary>
public sealed partial class Permission : IEquatable<Permission>
{
    public string Code { get; }

    public string Module { get; }

    public string Action { get; }

    private Permission(string code, string module, string action)
    {
        Code = code;
        Module = module;
        Action = action;
    }

    /// <summary>
    /// Creates a <see cref="Permission"/> after validating the <c>module.action</c> format.
    /// </summary>
    /// <exception cref="InvalidPermissionException">
    /// Thrown when <paramref name="code"/> is null, whitespace, or not in <c>module.action</c> form.
    /// Carries <c>MessageKey = nameof(Strings.Rbac_InvalidPermission)</c>.
    /// </exception>
    public static Permission Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidPermissionException(code ?? string.Empty);

        var trimmed = code.Trim();
        var match = PermissionCodeRegex().Match(trimmed);

        if (!match.Success)
            throw new InvalidPermissionException(trimmed);

        return new Permission(trimmed, match.Groups["module"].Value, match.Groups["action"].Value);
    }

    public override string ToString() => Code;

    public bool Equals(Permission? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Permission);

    public override int GetHashCode() => Code.GetHashCode(StringComparison.Ordinal);

    [GeneratedRegex(@"^(?<module>[a-z]+(?:_[a-z]+)*)\.(?<action>[a-z]+(?:_[a-z]+)*)$", RegexOptions.CultureInvariant)]
    private static partial Regex PermissionCodeRegex();
}
