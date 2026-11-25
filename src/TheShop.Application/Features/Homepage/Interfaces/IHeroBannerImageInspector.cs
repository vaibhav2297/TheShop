using TheShop.Application.Features.Homepage.DTOs;

namespace TheShop.Application.Features.Homepage.Interfaces;

/// <summary>Inspects actual image bytes without trusting upload metadata.</summary>
public interface IHeroBannerImageInspector
{
    /// <summary>Returns detected format and decoded dimensions, or an undecodable result.</summary>
    Task<HeroBannerImageInspection> InspectAsync(byte[] content, CancellationToken cancellationToken);
}

