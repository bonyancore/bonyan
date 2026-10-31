namespace Construction.Platform.Core.Application.Outbox;

/// <summary>
/// One dead-lettered outbox event (list / detail view). Spec 01 §4.5: <c>resolution</c> is
/// open/replayed/discarded, <c>error_class</c> is the exception class, and <c>resolution_note</c>
/// carries the who/why record (mandatory for a discard).
/// </summary>
public sealed record DeadOutboxMessageDto(
    Guid Id,
    Guid OriginalEventId,
    Guid TenantId,
    string Type,
    string Payload,
    string AggregateType,
    Guid AggregateId,
    long AggregateVersion,
    int Attempts,
    string? LastError,
    string? LastErrorCategory,
    string? ErrorClass,
    DateTimeOffset DeadAt,
    string Resolution,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNote,
    DateTimeOffset? ReplayedAt);

/// <summary>Request body for replaying a dead outbox message.</summary>
public sealed record ReplayDeadOutboxRequest(string Reason);

/// <summary>Request body for discarding a dead outbox message; the note is mandatory (BR-PLT-63).</summary>
public sealed record DiscardDeadOutboxRequest(string Note);

/// <summary>Response body for a successful replay.</summary>
public sealed record ReplayDeadOutboxResponse(
    Guid DeadMessageId,
    Guid NewOutboxId,
    DateTimeOffset ReplayedAt,
    Guid ReplayedBy);

/// <summary>Response body for a successful discard.</summary>
public sealed record DiscardDeadOutboxResponse(
    Guid DeadMessageId,
    DateTimeOffset ResolvedAt,
    Guid ResolvedBy,
    string ResolutionNote);

/// <summary>Outcome of a replay/discard attempt, so the endpoint can map 404 vs 409.</summary>
public enum DeadLetterResolutionState
{
    NotFound,
    AlreadyResolved,
    Resolved
}

public sealed record DeadLetterMutationResult(DeadLetterResolutionState State, Guid EventId, DateTimeOffset At)
{
    public static DeadLetterMutationResult NotFound() =>
        new(DeadLetterResolutionState.NotFound, Guid.Empty, default);

    public static DeadLetterMutationResult AlreadyResolved(Guid eventId) =>
        new(DeadLetterResolutionState.AlreadyResolved, eventId, default);
}

/// <summary>Operational outbox health for the admin endpoint (spec 01 §7.5 <c>GET /outbox/lag</c>).</summary>
public sealed record OutboxLagDto(double LagSeconds, long PendingCount);

public interface IDeadLetterStore
{
    Task<IReadOnlyList<DeadOutboxMessageDto>> ListAsync(
        Guid tenantId,
        string? eventType,
        string? resolution,
        DateTimeOffset? cursorDeadAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DeadOutboxMessageDto?> GetAsync(
        Guid tenantId,
        Guid deadMessageId,
        CancellationToken cancellationToken = default);

    Task<DeadLetterMutationResult> ReplayAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<DeadLetterMutationResult> DiscardAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string note,
        CancellationToken cancellationToken = default);
}

public interface IOutboxMetricsStore
{
    Task<OutboxLagDto> GetLagAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
