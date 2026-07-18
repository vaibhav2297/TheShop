namespace TheShop.Application.Common.Behaviors;

/// <summary>
/// Declares the permission code a MediatR command or query requires (FR-7). Read by
/// <see cref="AuthorizationBehavior{TRequest, TResponse}"/> before the handler runs; requests
/// with no attribute are not permission-gated at this layer.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiresPermissionAttribute(string permissionCode) : Attribute
{
    /// <summary>
    /// The required permission code in <c>module.action</c> form (e.g. <c>"orders.refund"</c>),
    /// matching a code from <c>PermissionCatalogue</c>.
    /// </summary>
    public string PermissionCode { get; } = permissionCode;
}
