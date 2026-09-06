using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Commands.UpdateProduct;

/// <summary>
/// Handles <see cref="UpdateProductCommand"/>. Loads the current aggregate, guards name and SKU
/// uniqueness excluding the product's own current values (RULE-2, RULE-8), invokes the
/// <see cref="Product"/> mutation methods for every integrity invariant, diffs the gallery to
/// upload new images and dispose removed ones, and persists guarded by
/// <see cref="UpdateProductCommand.RowVersion"/> (Decision 11).
/// </summary>
public sealed class UpdateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IBrandRepository brands,
    IFileStorage fileStorage) : IRequestHandler<UpdateProductCommand, Result<AdminProductDto>>
{
    /// <inheritdoc/>
    public async Task<Result<AdminProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var loaded = await products.GetForEditAsync(request.Id, cancellationToken);
        if (loaded is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.NotFound);

        var product = loaded.Value.Product;

        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.CategoryRequired);

        var brand = await brands.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.BrandRequired);

        var normalizedSkus = ProductInputMapper.CollectNormalizedSkus(request.Sku, request.Variants);
        var conflicts = await products.FindConflictsAsync(request.Name, normalizedSkus, request.Id, cancellationToken);
        if (conflicts.NameTaken)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.NameAlreadyExists);
        if (conflicts.SkusTaken.Count > 0)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.SkuAlreadyExists);

        var previousImageKeysById = product.Images.ToDictionary(image => image.Id, image => image.ObjectKey);
        var uploadedKeys = new List<string>();

        try
        {
            product.UpdateDetails(request.Name, request.Description ?? string.Empty, category, brand);
            product.SetSku(Sku.Create(request.Sku));
            product.SetPricing(ProductInputMapper.ToPricing(request.OriginalPrice, request.SalePrice));
            product.SetPublished(request.IsPublished);

            var gallery = new List<GalleryImageInput>();
            var clientIds = new List<Guid>();
            var keptImageIds = new HashSet<Guid>();
            foreach (var entry in request.Gallery.OrderBy(e => e.Position))
            {
                if (entry.Upload is { } upload)
                {
                    using var content = new MemoryStream(upload.Content);
                    var key = await fileStorage.UploadAsync(
                        StorageArea.ProductImages, product.Id, content, upload.FileName, upload.ContentType, cancellationToken);
                    uploadedKeys.Add(key);
                    gallery.Add(new GalleryImageInput(null, key, entry.IsPrimary));
                    clientIds.Add(entry.ClientId);
                }
                else if (entry.ImageId is Guid imageId && previousImageKeysById.TryGetValue(imageId, out var existingKey))
                {
                    keptImageIds.Add(imageId);
                    gallery.Add(new GalleryImageInput(imageId, existingKey, entry.IsPrimary));
                    clientIds.Add(entry.ClientId);
                }
            }

            var removedImageKeys = previousImageKeysById
                .Where(kvp => !keptImageIds.Contains(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            product.SetGallery(gallery);

            // The gallery keeps the order it was given, so each client identifier lines up with the
            // image the aggregate now holds for it — the stored identifier for a kept image, the
            // freshly minted one for an upload. Resolving pins through this map is what lets a staff
            // member pin an image they picked in the same unsaved session, without any
            // client-supplied value ever becoming an image's real identity.
            var imageIdByClientId = clientIds
                .Select((clientId, index) => (clientId, imageId: product.Images[index].Id))
                .ToDictionary(pair => pair.clientId, pair => pair.imageId);

            product.SetOptionTypes(ProductInputMapper.ToOptionTypeInputs(request.OptionTypes));
            product.ApplyVariantConfiguration(
                ProductInputMapper.ToVariantInputs(request.Variants, imageIdByClientId));

            if (request.IsPublished)
                product.EnsurePublishable();

            var updateResult = await products.UpdateAsync(product, request.RowVersion, cancellationToken);
            if (updateResult.IsFailure)
            {
                await TryDeleteImagesAsync(uploadedKeys, cancellationToken);
                return Result.Fail<AdminProductDto>(updateResult.Error!);
            }

            await TryDeleteImagesAsync(removedImageKeys, cancellationToken);

            return Result.Ok(AdminProductDtoMapper.ToAdminDto(product, fileStorage, updateResult.Value));
        }
        catch (ProductNotPublishableException ex)
        {
            // Carried separately from the message key so the form can flag the exact field or
            // variant row rather than only saying that something is missing (AC-20).
            await TryDeleteImagesAsync(uploadedKeys, cancellationToken);
            return Result.Fail<AdminProductDto>(ex.MessageKey, ex.Missing);
        }
        catch (DomainException ex)
        {
            await TryDeleteImagesAsync(uploadedKeys, cancellationToken);
            return Result.Fail<AdminProductDto>(ex.MessageKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await TryDeleteImagesAsync(uploadedKeys, cancellationToken);
            return Result.Fail<AdminProductDto>(ProductErrorKeys.UpdateFailed);
        }
    }

    private async Task TryDeleteImagesAsync(IReadOnlyList<string> objectKeys, CancellationToken cancellationToken)
    {
        foreach (var key in objectKeys)
        {
            try
            {
                await fileStorage.DeleteAsync(StorageArea.ProductImages, key, cancellationToken);
            }
            catch
            {
                // Best-effort compensation (RULE-18/RULE-11): an orphaned storage object is
                // acceptable; failing this request a second time over cleanup is not.
            }
        }
    }
}
