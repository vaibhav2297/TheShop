namespace TheShop.Application.Common.Models;

/// <summary>
/// A discriminated success/failure value returned by Application layer handlers.
/// On failure, <see cref="Error"/> carries a resource key from <c>Strings.resx</c>
/// that the UI resolves via <c>Localizer[result.Error]</c>.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    /// <summary>
    /// The resource key describing the failure, or <c>null</c> on success.
    /// </summary>
    public string? Error { get; }
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A successful result cannot carry an error key.");

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("A failed result must carry a non-empty error key.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(string errorKey) => new(false, errorKey);

    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Fail<T>(string errorKey) => Result<T>.Fail(errorKey);
}

/// <summary>
/// A <see cref="Result"/> that also carries a strongly-typed payload on success.
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// The success payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    private Result(bool isSuccess, T? value, string? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static new Result<T> Fail(string errorKey) => new(false, default, errorKey);
}
