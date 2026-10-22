using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetProductDetail;

/// <summary>Rejects empty product identifiers through the standard validation pipeline.</summary>
public sealed class GetProductDetailQueryValidator : AbstractValidator<GetProductDetailQuery>
{
    /// <summary>Uses the same unavailable key for invalid and inaccessible product identifiers.</summary>
    public GetProductDetailQueryValidator()
    {
        RuleFor(q => q.Id).NotEmpty().WithMessage(ProductErrorKeys.DetailUnavailable);
    }
}
