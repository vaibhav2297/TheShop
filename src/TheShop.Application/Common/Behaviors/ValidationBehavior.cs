using FluentValidation;
using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs all registered <see cref="IValidator{T}"/> instances
/// before the handler. When <typeparamref name="TResponse"/> is <see cref="Result"/> or
/// <see cref="Result{T}"/>, the first validation error is returned as a failure result
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

        var firstErrorKey = failures[0].ErrorMessage;

        if (TryBuildFailureResult(firstErrorKey, out var failure))
            return (TResponse)failure!;

        throw new ValidationException(failures);
    }

    private static bool TryBuildFailureResult(string errorKey, out object? failure)
    {
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            failure = Result.Fail(errorKey);
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
                            && m.GetParameters().Length == 1);
            failure = failMethod.MakeGenericMethod(valueType).Invoke(null, [errorKey]);
            return true;
        }

        failure = null;
        return false;
    }
}
