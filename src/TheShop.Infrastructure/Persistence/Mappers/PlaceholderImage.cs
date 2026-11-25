namespace TheShop.Infrastructure.Persistence.Mappers;

/// <summary>
/// Generates a placeholder image URL for records that have no uploaded image, keyed by a
/// human-readable label so the placeholder is at least visually distinguishable.
/// </summary>
internal static class PlaceholderImage
{
    public static string For(string label) =>
        $"https://placehold.co/400x400/E8E8E8/7A7A7A?text={Uri.EscapeDataString(label)}&font=raleway";
}
