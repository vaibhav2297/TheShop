using System.Reflection;
using MediatR;
using TheShop.Application.Common.Interfaces;
using TheShop.Application.Common.Models;
using TheShop.Application.Features.Roles;

namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces <see cref="RequiresPermissionAttribute"/> on every
/// command and query. Requests with no attribute pass through unchecked. For a decorated
/// request, the check reads the current user's permission claims via
/// <see cref="ICurrentUserService.HasPermission"/> — claims are minted into the access token by
/// the database's custom access token hook, so staleness is bounded by the token TTL and no
/// per-request database read is needed; Supabase RLS remains the authoritative boundary. A
/// missing permission (including an unauthenticated caller, who holds no permission claims)
/// returns <see cref="Result.Fail(string)"/> with <see cref="RbacErrorKeys.AccessDenied"/>
/// instead of throwing — this is an expected business outcome, not a technical failure.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(ICurrentUserService currentUser)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly RequiresPermissionAttribute? Requirement =
        typeof(TRequest).GetCustomAttribute<RequiresPermissionAttribute>();

    /// <summary>
    /// Checks the required permission, if any, and either delegates to the next handler in the
    /// pipeline or short-circuits with an access-denied failure result.
    /// </summary>
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (Requirement is null)
            return await next(cancellationToken);

        if (currentUser.HasPermission(Requirement.PermissionCode))
            return await next(cancellationToken);

        if (TryBuildFailureResult(RbacErrorKeys.AccessDenied, out var failure))
            return (TResponse)failure!;

        throw new InvalidOperationException(
            $"{typeof(TRequest).Name} is decorated with [RequiresPermission] but its response " +
            $"type {typeof(TResponse)} is neither Result nor Result<T>.");
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
