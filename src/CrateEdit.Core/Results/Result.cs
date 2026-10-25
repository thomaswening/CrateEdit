using System.Diagnostics.CodeAnalysis;

namespace CrateEdit.Core.Results;

/// <summary>
/// The outcome of an operation without a return value: success, or an <see cref="Results.Error"/>.
/// An error converts implicitly, so a method can simply <c>return new SomeError(...)</c>.
/// </summary>
public sealed class Result
{
    private Result(Error? error) => Error = error;

    public static Result Success { get; } = new(null);

    /// <summary>The error, or null on success.</summary>
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public bool IsSuccess => !IsFailure;

    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "A null error is a programming error. Turning it into a success would hide it.")]
    public static implicit operator Result(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/> on success, or an
/// <see cref="Results.Error"/>. Both a value and an error convert implicitly.
/// </summary>
/// <example>
/// <code>
/// if (ReadSomething() is { IsFailure: true } failed) return failed.Error;   // propagate
/// </code>
/// </example>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>The error, or null on success.</summary>
    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public bool IsSuccess => !IsFailure;

    /// <summary>The value. Reading it from a failed result is a programming error and throws.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value: {Error}");

    public static implicit operator Result<T>(T value) => new(value, null);

    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "A null error is a programming error. Turning it into a success would hide it.")]
    public static implicit operator Result<T>(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);

    public override string ToString() => IsSuccess ? $"Success: {_value}" : $"Failure: {Error}";
}
