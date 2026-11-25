using FluentValidation;

namespace TheShop.Application.Features.Products.Queries.GetProductDetail;

/// <summary>Rejects empty product identifiers through the standard validation pipeline.</summary>
public sealed class GetProductDetailQueryValidator : AbstractValidator<GetProductDetailQuery>
{
    /// <summary>Uses the same unavailable key for invalid and inaccessible product identifiers.</summary>
    public GetProductDetailQueryValidator()
    {
        RuleFor(q => q)
            .Must(q => (q.Id is { } id && id != Guid.Empty && q.ProductSlug is null && q.VariantSlug is null) ||
                (q.Id is null && IsSlug(q.ProductSlug) && (q.VariantSlug is null || IsSlug(q.VariantSlug))))
            .WithMessage(ProductErrorKeys.DetailUnavailable);
    }

    private static bool IsSlug(string? value) => value is not null &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(-[a-z0-9]+)*$");
}
