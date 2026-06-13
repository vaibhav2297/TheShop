using MediatR;
using TheShop.Application.Common.Models;

namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that converts infrastructure exceptions into failure
/// <see cref="Result"/> values so they never reach the UI as unhandled exceptions.
/// Only applies when <typeparamref name="TResponse"/> is <see cref="Result"/> or
/// <see cref="Result{T}"/>; for all other response types the exception is re-thrown.
/// </summary>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Resx keys defined in Strings.resx / Strings.fr.resx.
    private const string NetworkErrorKey = "Auth_Network";
    private const string UnexpectedErrorKey = "Auth_Unexpected";

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (HttpRequestException) when (TryBuildFailureResult(NetworkErrorKey, out var failure))
        {
            return (TResponse)failure!;
        }
        catch (Exception) when (TryBuildFailureResult(UnexpectedErrorKey, out var failure))
        {
            return (TResponse)failure!;
        }
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
