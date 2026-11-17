namespace Construction.Kernel;

public enum ErrorType { Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden, RateLimited }

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public const string RateLimitExceededCode = "rate_limit_exceeded";

    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message)      => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message)   => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message)     => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message)     => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message)    => new(code, message, ErrorType.Forbidden);

    /// <summary>
    /// Creates a rate-limit error with the immutable code <c>"rate_limit_exceeded"</c>.
    /// </summary>
    public static Error RateLimited(string message) =>
        new(RateLimitExceededCode, message, ErrorType.RateLimited);
}
