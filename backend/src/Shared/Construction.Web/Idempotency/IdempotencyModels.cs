namespace Construction.Web.Idempotency;

/// <summary>Scope of an idempotency key: tenant + principal + key (spec 01 §4.13).</summary>
public sealed record IdempotencyScope(
    Guid TenantId,
    Guid PrincipalId,
    string Key,
    string HttpMethod,
    string PathTemplate,
    string PrincipalType = "user");

/// <summary>One persisted idempotency row, reduced to what a replay decision needs.</summary>
public sealed record IdempotencyRecord(
    string Status,
    byte[] RequestHash,
    int? ResponseStatus,
    string? ContentType,
    byte[]? ResponseBody,
    bool ReplayTooLarge,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt)
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";

    public bool IsCompleted => string.Equals(Status, Completed, StringComparison.Ordinal);

    public bool IsInProgress => string.Equals(Status, InProgress, StringComparison.Ordinal);
}

public enum IdempotencyDecisionKind
{
    Proceed,
    Replay,
    ConflictInProgress,
    KeyReuse,
    ReplayTooLarge
}

public sealed record IdempotencyDecision(IdempotencyDecisionKind Kind, IdempotencyRecord? Record = null)
{
    public static IdempotencyDecision Proceed { get; } = new(IdempotencyDecisionKind.Proceed);

    public static IdempotencyDecision Conflict { get; } =
        new(IdempotencyDecisionKind.ConflictInProgress);

    public static IdempotencyDecision Reuse { get; } = new(IdempotencyDecisionKind.KeyReuse);

    public static IdempotencyDecision Replay(IdempotencyRecord record) =>
        new(IdempotencyDecisionKind.Replay, record);

    public static IdempotencyDecision TooLarge(IdempotencyRecord record) =>
        new(IdempotencyDecisionKind.ReplayTooLarge, record);
}
