using FluentValidation;

namespace TheShop.Application.Features.Products.Commands.DeleteProducts;

/// <summary>
/// Validates <see cref="DeleteProductsCommand"/>: at least one id, no empty guids, no duplicates.
/// </summary>
public sealed class DeleteProductsCommandValidator : AbstractValidator<DeleteProductsCommand>
{
    public DeleteProductsCommandValidator()
    {
        RuleFor(x => x.ProductIds)
            .NotEmpty().WithMessage(ProductErrorKeys.ProductIdsRequired);

        RuleForEach(x => x.ProductIds)
            .NotEmpty().WithMessage(ProductErrorKeys.ProductIdsRequired);

        RuleFor(x => x.ProductIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage(ProductErrorKeys.ProductIdsRequired);
    }
}
