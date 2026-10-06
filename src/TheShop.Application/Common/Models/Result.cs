namespace TheShop.Application.Common.Models;

/// <summary>
/// A discriminated success/failure value returned by Application layer handlers.
/// On failure, <see cref="Error"/> carries a resource key from <c>Strings.resx</c>
/// that the UI resolves via <c>Localizer[result.Error]</c>.
/// </summary>
public class Result
{
    /// <summary>
    /// True if the operation succeeded; false if it failed.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// The resource key describing the failure, or <c>null</c> on success.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Machine-readable detail about the failure, for the rare error whose message alone cannot say
    /// enough — chiefly a publish refusal, which has to name every value that is missing so the UI
    /// can flag the exact field or row (RULE-14, AC-20). Empty for a failure that needs no detail.
    /// </summary>
    public IReadOnlyList<string> ErrorArgs { get; }

    /// <summary>
    /// All request validation failures; empty for success and ordinary business failures.
    /// </summary>
    public IReadOnlyList<FieldValidationError> ValidationErrors { get; }

    /// <summary>
    /// True if the operation failed; false if it succeeded.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Initializes a new Result with outcome and optional error details.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">Resource key on failure; must be null on success.</param>
    /// <param name="errorArgs">Machine-readable failure details; ignored on success.</param>
    /// <param name="validationErrors">Request validation failures; empty for success and ordinary business failures.</param>
    /// <exception cref="InvalidOperationException">Thrown if success and error are both present, or if failed but error is empty.</exception>
    protected Result(bool isSuccess, string? error, IReadOnlyList<string>? errorArgs = null,
        IReadOnlyList<FieldValidationError>? validationErrors = null)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A successful result cannot carry an error key.");

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("A failed result must carry a non-empty error key.");

        IsSuccess = isSuccess;
        Error = error;
        ErrorArgs = errorArgs ?? [];
        ValidationErrors = validationErrors is null ? [] : Array.AsReadOnly(validationErrors.ToArray());
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Ok() => new(true, null);

    /// <summary>
    /// Creates a failed result with an error key.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    public static Result Fail(string errorKey) => new(false, errorKey);

    /// <summary>
    /// Creates a failed result with an error key and machine-readable details.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    public static Result Fail(string errorKey, IReadOnlyList<string> errorArgs) => new(false, errorKey, errorArgs);

    /// <summary>
    /// Creates a failed result with an error key, machine-readable details, and validation errors.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    /// <param name="validationErrors">Request validation failures for specific fields.</param>
    public static Result Fail(string errorKey, IReadOnlyList<string> errorArgs,
        IReadOnlyList<FieldValidationError> validationErrors) => new(false, errorKey, errorArgs, validationErrors);

    /// <summary>
    /// Creates a successful result with a typed payload.
    /// </summary>
    /// <typeparam name="T">The type of the success payload.</typeparam>
    /// <param name="value">The payload to return on success.</param>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>
    /// Creates a failed typed result with an error key.
    /// </summary>
    /// <typeparam name="T">The type of the success payload (unused).</typeparam>
    /// <param name="errorKey">Resource key describing the failure.</param>
    public static Result<T> Fail<T>(string errorKey) => Result<T>.Fail(errorKey);

    /// <summary>
    /// Creates a failed typed result with an error key and machine-readable details.
    /// </summary>
    /// <typeparam name="T">The type of the success payload (unused).</typeparam>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    public static Result<T> Fail<T>(string errorKey, IReadOnlyList<string> errorArgs) => Result<T>.Fail(errorKey, errorArgs);

    /// <summary>
    /// Creates a failed typed result with an error key, machine-readable details, and validation errors.
    /// </summary>
    /// <typeparam name="T">The type of the success payload (unused).</typeparam>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    /// <param name="validationErrors">Request validation failures for specific fields.</param>
    public static Result<T> Fail<T>(string errorKey, IReadOnlyList<string> errorArgs,
        IReadOnlyList<FieldValidationError> validationErrors) => Result<T>.Fail(errorKey, errorArgs, validationErrors);
}

/// <summary>
/// A <see cref="Result"/> that also carries a strongly-typed payload on success.
/// </summary>
/// <summary>
/// A Result that carries a strongly-typed payload on success.
/// </summary>
/// <typeparam name="T">The type of the success payload.</typeparam>
public class Result<T> : Result
{
    /// <summary>
    /// Holds the success payload, or the default value on failure.
    /// </summary>
    private readonly T? _value;

    /// <summary>
    /// The success payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public T Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    /// <summary>
    /// Initializes a new typed Result with outcome, payload, and optional error details.
    /// </summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="value">The success payload; ignored on failure.</param>
    /// <param name="error">Resource key on failure; must be null on success.</param>
    /// <param name="errorArgs">Machine-readable failure details; ignored on success.</param>
    /// <param name="validationErrors">Request validation failures; empty for success and ordinary business failures.</param>
    private Result(bool isSuccess, T? value, string? error, IReadOnlyList<string>? errorArgs = null,
        IReadOnlyList<FieldValidationError>? validationErrors = null)
        : base(isSuccess, error, errorArgs, validationErrors)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a successful result with a typed payload.
    /// </summary>
    /// <param name="value">The payload to return on success.</param>
    public static Result<T> Ok(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with an error key and no payload.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    public static new Result<T> Fail(string errorKey) => new(false, default, errorKey);

    /// <summary>
    /// Creates a failed result with an error key, machine-readable details, and no payload.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    public static new Result<T> Fail(string errorKey, IReadOnlyList<string> errorArgs) => new(false, default, errorKey, errorArgs);

    /// <summary>
    /// Creates a failed result with an error key, machine-readable details, validation errors, and no payload.
    /// </summary>
    /// <param name="errorKey">Resource key describing the failure.</param>
    /// <param name="errorArgs">Machine-readable details about the failure.</param>
    /// <param name="validationErrors">Request validation failures for specific fields.</param>
    public static new Result<T> Fail(string errorKey, IReadOnlyList<string> errorArgs,
        IReadOnlyList<FieldValidationError> validationErrors) => new(false, default, errorKey, errorArgs, validationErrors);
}
