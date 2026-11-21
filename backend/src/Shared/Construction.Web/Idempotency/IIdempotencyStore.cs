namespace Construction.Web.Idempotency;

/// <summary>
/// Persistence port for idempotency keys (spec 01 §4.13 <c>platform.idempotency_keys</c>).
/// Implementations must be atomic: exactly one concurrent claimant wins.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Inserts an <c>in_progress</c> claim, or takes over an expired one.
    /// Returns true when the caller owns the claim.
    /// </summary>
    Task<bool> TryClaimAsync(
        IdempotencyScope scope,
        byte[] requestHash,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the live row for a scope, or null when none exists.</summary>
    Task<IdempotencyRecord?> FindAsync(
        IdempotencyScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare-and-swap takeover of an abandoned (<c>in_progress</c> older than the threshold) claim.
    /// </summary>
    Task<bool> TryTakeOverAsync(
        IdempotencyScope scope,
        TimeSpan staleAfter,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    /// <summary>Stores the response and marks the key completed.</summary>
    Task CompleteAsync(
        IdempotencyScope scope,
        int statusCode,
        string? contentType,
        byte[]? body,
        IReadOnlyDictionary<string, string> headers,
        bool tooLarge,
        CancellationToken cancellationToken = default);

    /// <summary>Removes an <c>in_progress</c> claim so the client can retry (5xx / exception).</summary>
    Task ReleaseAsync(IdempotencyScope scope, CancellationToken cancellationToken = default);

    /// <summary>Deletes expired rows in batches; returns the number removed.</summary>
    Task<int> PurgeExpiredAsync(int batchSize, CancellationToken cancellationToken = default);
}
