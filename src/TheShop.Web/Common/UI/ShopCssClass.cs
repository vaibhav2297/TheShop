using System.Runtime.CompilerServices;
using System.Text.Json;

namespace TheShop.Web.Common.UI;

/// <summary>Composes component class names without introducing a styling framework.</summary>
public static class ShopCssClass
{
    /// <summary>Joins nonblank class groups with a single separating space.</summary>
    public static string Join(params string?[] classes) =>
        string.Join(" ", classes.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

    /// <summary>Creates a component-prefixed kebab-case modifier from a declared enum value.</summary>
    /// <param name="prefix">One component class, such as shop-button; not a CSS selector or class list.</param>
    /// <param name="value">A named visual option whose corresponding styles are owned by the component.</param>
    /// <param name="parameterName">Compiler-supplied caller expression used for invalid-value diagnostics.</param>
    /// <exception cref="ArgumentException">The prefix is blank or contains whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value is not declared by its enum.</exception>
    public static string Modifier<TEnum>(string prefix, TEnum value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where TEnum : struct, Enum
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (prefix.Any(char.IsWhiteSpace))
            throw new ArgumentException("Expected a single component class.", nameof(prefix));
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "Expected a declared visual option.");

        return $"{prefix}-{JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString())}";
    }
}
