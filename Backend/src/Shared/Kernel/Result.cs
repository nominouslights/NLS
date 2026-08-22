namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Outcome of an operation that can fail with a domain <see cref="Error"/>.
/// Preferred over exceptions for expected failure paths (validation, conflicts, not-found).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>
/// A <see cref="Result"/> carrying a value on success.
/// </summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error, PageInfo? pageInfo = null)
        : base(isSuccess, error)
    {
        _value = value;
        PageInfo = pageInfo;
    }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    /// <summary>
    /// Set when <see cref="Value"/> is one page of a larger set; null for whole-set results.
    /// </summary>
    public PageInfo? PageInfo { get; }

    /// <summary>
    /// Attaches paging metadata to a successful result. A no-op on failure — a failed result
    /// has no value to page.
    /// </summary>
    public Result<TValue> WithPage(PageInfo pageInfo) => IsSuccess
        ? new Result<TValue>(_value, true, Error.None, pageInfo)
        : this;
}
