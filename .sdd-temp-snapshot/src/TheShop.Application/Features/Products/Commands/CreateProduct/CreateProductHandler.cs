using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Common.Storage;
using TheShop.Application.Features.Products.DTOs;
using TheShop.Application.Features.Products.Mappers;
using TheShop.Domain.Entities;
using TheShop.Domain.Exceptions;
using TheShop.Domain.ValueObjects;

namespace TheShop.Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Handles <see cref="CreateProductCommand"/>. Guards name and SKU uniqueness across the whole
/// catalogue (RULE-2, RULE-8), invokes the <see cref="Product"/> aggregate methods for every
/// integrity invariant, uploads gallery images, and persists the aggregate in one transaction.
/// Best-effort deletes every image uploaded this request if persistence fails afterwards, so no
/// orphaned object survives an abandoned or rejected save (RULE-18).
/// </summary>
public sealed class CreateProductHandler(
    IProductRepository products,
    ICategoryRepository categories,
    IBrandRepository brands,
    IFileStorage fileStorage) : IRequestHandler<CreateProductCommand, Result<AdminProductDto>>
{
    /// <inheritdoc/>
    public async Task<Result<AdminProductDto>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.CategoryRequired);

        var brand = await brands.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand is null)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.BrandRequired);

        var normalizedSkus = ProductInputMapper.CollectNormalizedSkus(request.Sku, request.Variants);
        var conflicts = await products.FindConflictsAsync(request.Name, normalizedSkus, excludeProductId: null, cancellationToken);
        if (conflicts.NameTaken)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.NameAlreadyExists);
        if (conflicts.SkusTaken.Count > 0)
            return Result.Fail<AdminProductDto>(ProductErrorKeys.SkuAlreadyExists);

        Product product;
        try
        {
            product = Product.Create(
                request.Name,
                ProductDescription.Create(request.Description),
                imageUrl: null,
                Sku.Create(request.Sku),
                ProductInputMapper.ToPricing(request.OriginalPrice, request.SalePrice),
                request.IsPublished,
                category,
                brand);
        }
        catch (DomainException ex)
        {
            return Result.Fail<AdminProductDto>(ex.MessageKey);
        }

        var uploadedKeys = new List<string>();
        try
        {
            var gallery = new List<GalleryImageInput>();
            var clientIds = new List<Guid>();
            foreach (var entry in request.Gallery.OrderBy(e => e.Position))
            {
                if (entry.Upload is not { } upload)
                    continue;

                using var content = new MemoryStream(upload.Content);
                var key = await fileStorage.UploadAsync(
                    StorageArea.ProductImages, product.Id, content, upload.FileName, upload.ContentType, cancellationToken);
                uploadedKeys.Add(key);
                gallery.Add(new GalleryImageInput(null, key, entry.IsPrimary));
                clientIds.Add(entry.ClientId);
            }

            product.SetGallery(gallery);

            // The gallery keeps the order it was given, so each client identifier lines up with the
            // image the aggregate just minted for it. Resolving pins through this map is what lets a
            // staff member pin an image they picked in the same unsaved session, without any
            // client-supplied value ever becoming an image's real identity.
            var imageIdByClientId = clientIds
                .Select((clientId, index) => (clientId, imageId: product.Images[index].Id))
                .ToDictionary(pair => pair.clientId, pair => pair.imageId);

            product.SetOptionTypes(ProductInputMapper.ToOptionTypeInputs(request.OptionTypes));
            product.ApplyVariantConfiguration(
                ProductInputMapper.ToVariantInputs(request.Variants, imageIdByClientId));
            product.SetSpecifications(ProductInputMapper.ToSpecificationInputs(request.Specifications));

            if (request.IsPublished)
                product.EnsurePublishable();

            var addResult = await products.AddAsync(product, cancellationToken);
            if (addResult.IsFailure)
            {
                await TryDeleteImagesAsync(uploadedKeys, cancellationToken);
                return Result.Fail<AdminProductDto>(addResult.Error!);
            }

            return Result.Ok(AdminProductDtoMapper.ToAdminDto(product, fileStorage, addResult.Value));
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
            return Result.Fail<AdminProductDto>(ProductErrorKeys.CreateFailed);
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
                // Best-effort compensation (RULE-18): an orphaned storage object is acceptable;
                // failing this request a second time over cleanup is not.
            }
        }
    }
}
