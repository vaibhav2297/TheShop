namespace TheShop.Application.Common.Models;

/// <summary>
/// A request property and resource key describing a validation failure.
/// </summary>
/// <param name="PropertyName">The command property path, or an empty path for a request-level error.</param>
/// <param name="MessageKey">The resource key resolved by the presentation layer.</param>
public sealed record FieldValidationError(string PropertyName, string MessageKey);
