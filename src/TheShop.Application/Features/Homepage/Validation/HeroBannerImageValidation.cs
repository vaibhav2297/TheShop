using TheShop.Application.Common.Models;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;

namespace TheShop.Application.Features.Homepage.Validation;

/// <summary>Shared byte and geometry validation used by selection and save requests.</summary>
internal static class HeroBannerImageValidation
{
    internal static async Task<string?> ErrorAsync(ImageUpload image, HeroBannerImageRole role,
        IHeroBannerImageInspector inspector, CancellationToken cancellationToken)
    {
        if (image.Content is not { Length: > 0 }) return HeroBannerErrorKeys.ImageUnreadable;
        if (image.Content.Length > 5_242_880) return HeroBannerErrorKeys.ImageTooLarge;
        if (image.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            return HeroBannerErrorKeys.ImageInvalidType;
        var inspection = await inspector.InspectAsync(image.Content, cancellationToken);
        if (inspection.ContentType is null || inspection.ContentType != image.ContentType)
            return HeroBannerErrorKeys.ImageInvalidType;
        if (!inspection.IsDecodable || inspection.Width <= 0 || inspection.Height <= 0)
            return HeroBannerErrorKeys.ImageUnreadable;
        if (role == HeroBannerImageRole.Desktop)
            return inspection.Width < 1920 || inspection.Height < 1080
                || (long)inspection.Width * 9 != (long)inspection.Height * 16
                ? HeroBannerErrorKeys.DesktopDimensionsInvalid : null;
        return (long)inspection.Width * 5 != (long)inspection.Height * 4
            ? HeroBannerErrorKeys.MobileDimensionsInvalid : null;
    }
}

