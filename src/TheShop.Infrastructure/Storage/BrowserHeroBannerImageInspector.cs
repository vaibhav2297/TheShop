using Microsoft.JSInterop;
using TheShop.Application.Features.Homepage.DTOs;
using TheShop.Application.Features.Homepage.Interfaces;

namespace TheShop.Infrastructure.Storage;

/// <summary>Uses browser decoding and actual format signatures for admin image validation.</summary>
public sealed class BrowserHeroBannerImageInspector(IJSRuntime js) : IHeroBannerImageInspector
{
    /// <inheritdoc/>
    public async Task<HeroBannerImageInspection> InspectAsync(byte[] content, CancellationToken cancellationToken)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", cancellationToken,
                "./js/heroBannerImages.js");
            return await module.InvokeAsync<HeroBannerImageInspection>("inspect", cancellationToken, content);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (JSException) { return new(null, 0, 0, false); }
    }
}

