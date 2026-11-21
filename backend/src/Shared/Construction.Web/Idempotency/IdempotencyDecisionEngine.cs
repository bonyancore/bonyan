namespace Construction.Web.Idempotency;

/// <summary>
/// Pure replay decision for an existing idempotency row (T-205-U04). Kept free of I/O so every
/// branch is unit-testable.
/// </summary>
public static class IdempotencyDecisionEngine
{
    public static IdempotencyDecision Decide(
        IdempotencyRecord? existing,
        byte[] requestHash,
        DateTimeOffset now,
        TimeSpan takeoverAfter)
    {
        if (existing is null)
        {
            return IdempotencyDecision.Proceed;
        }

        var sameHash = RequestHasher.HashesEqual(existing.RequestHash, requestHash);

        if (existing.IsCompleted)
        {
            if (!sameHash)
            {
                return IdempotencyDecision.Reuse;
            }

            return existing.ReplayTooLarge
                ? IdempotencyDecision.TooLarge(existing)
                : IdempotencyDecision.Replay(existing);
        }

        if (existing.IsInProgress)
        {
            if (!sameHash)
            {
                return IdempotencyDecision.Reuse;
            }

            var age = now - existing.CreatedAt;
            return age >= takeoverAfter ? IdempotencyDecision.Proceed : IdempotencyDecision.Conflict;
        }

        // Unknown state: treat as a new request rather than blocking the client forever.
        return IdempotencyDecision.Proceed;
    }
}
