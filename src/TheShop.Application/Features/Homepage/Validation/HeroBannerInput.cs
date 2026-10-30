namespace TheShop.Application.Features.Homepage.Validation;

/// <summary>Shared normalization applied when constructing banner requests, before validation.</summary>
internal static class HeroBannerInput
{
    internal static string Required(string? value) => value?.Trim() ?? string.Empty;
    internal static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool IsDestination(string? value)
    {
        if (value is null) return true;
        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')) return false;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            if (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2]))
                return false;
            index += 2;
        }
        if (value.StartsWith('/')) return !value.StartsWith("//", StringComparison.Ordinal)
            && Uri.TryCreate(value, UriKind.Relative, out _);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }
}
