using FluentValidation;
using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs all registered <see cref="IValidator{T}"/> instances
/// before the handler. When <typeparamref name="TResponse"/> is <see cref="Result"/> or
/// <see cref="Result{T}"/>, all validation errors are returned with the first error as the primary failure
/// instead of throwing. For all other response types, a <see cref="ValidationException"/> is thrown.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>
    /// Validates the request and, if valid, delegates to the next handler in the pipeline.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0)
            return await next(cancellationToken);

        var first = failures[0];
        var errorArgs = (IReadOnlyList<string>?)(first.CustomState as IReadOnlyList<string>) ?? [];
        var validationErrors = failures
            .Select(f => new FieldValidationError(f.PropertyName, f.ErrorMessage))
            .ToArray();

        if (TryBuildFailureResult(first.ErrorMessage, errorArgs, validationErrors, out var failure))
            return (TResponse)failure!;

        throw new ValidationException(failures);
    }

    private static bool TryBuildFailureResult(string errorKey, IReadOnlyList<string> errorArgs,
        IReadOnlyList<FieldValidationError> validationErrors, out object? failure)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            failure = Result.Fail(errorKey, errorArgs, validationErrors);
            return true;
        }

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Fail)
                            && m.IsGenericMethod
                            && m.GetParameters().Length == 3);
            failure = failMethod.MakeGenericMethod(valueType).Invoke(null, [errorKey, errorArgs, validationErrors]);
            return true;
        }

        failure = null;
        return false;
    }
}
