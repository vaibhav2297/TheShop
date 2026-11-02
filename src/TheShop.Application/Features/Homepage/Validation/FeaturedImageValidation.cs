using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.Interfaces;

namespace TheShop.Application.Features.Homepage.Validation;

internal static class FeaturedImageValidation
{
    internal static async Task<string?> ErrorAsync(ImageUpload image,
        IHeroBannerImageInspector inspector, CancellationToken cancellationToken)
    {
        if (image.Content is not { Length: > 0 }) return FeaturedErrorKeys.ImageUnreadable;
        if (image.Content.Length > 5_242_880) return FeaturedErrorKeys.ImageTooLarge;
        if (image.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            return FeaturedErrorKeys.ImageInvalidType;
        var inspection = await inspector.InspectAsync(image.Content, cancellationToken);
        if (inspection.ContentType is null || inspection.ContentType != image.ContentType)
            return FeaturedErrorKeys.ImageInvalidType;
        if (!inspection.IsDecodable || inspection.Width <= 0 || inspection.Height <= 0)
            return FeaturedErrorKeys.ImageUnreadable;
        return inspection.Width < 1200 || inspection.Width != inspection.Height
            ? FeaturedErrorKeys.ImageDimensionsInvalid : null;
    }
}
