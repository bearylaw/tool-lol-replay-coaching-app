using System.Diagnostics.CodeAnalysis;

namespace RiftReview.Core.Results;

/// <summary>
/// The outcome of an operation that can fail in an expected way: either a value or an error,
/// never both. Bugs throw instead.
/// </summary>
[SuppressMessage("Design", "CA1000", Justification = "Success/Failure factories are the point of a result type.")]
public sealed class Result<TValue, TError>
    where TValue : notnull
    where TError : notnull
{
    private readonly TValue? value;
    private readonly TError? error;

    private Result(TValue? value, TError? error, bool isSuccess)
    {
        this.value = value;
        this.error = error;
        IsSuccess = isSuccess;
    }

    /// <summary>True when the operation produced a value.</summary>
    public bool IsSuccess { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result<TValue, TError> Success(TValue value) => new(value, default, true);

    /// <summary>Creates a failed result.</summary>
    public static Result<TValue, TError> Failure(TError error) => new(default, error, false);

    /// <summary>Gets the value, or the error when there is none.</summary>
    public bool TryGetValue([NotNullWhen(true)] out TValue? value, [NotNullWhen(false)] out TError? error)
    {
        value = this.value;
        error = this.error;
        return IsSuccess;
    }

    /// <summary>Folds both cases into one value.</summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(value!) : onFailure(error!);
    }
}
