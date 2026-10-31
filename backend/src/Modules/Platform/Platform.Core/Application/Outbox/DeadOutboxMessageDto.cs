namespace Construction.Platform.Core.Application.Outbox;

/// <summary>
/// DTO for a dead outbox message entry (list / detail view).
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
    DateTimeOffset DeadAt,
    DateTimeOffset? ReplayedAt,
    Guid? ReplayedBy);

/// <summary>
/// Request body for replaying a dead outbox message.
/// </summary>
public sealed record ReplayDeadOutboxRequest(string Reason);

/// <summary>
/// Response body for a successful replay.
/// </summary>
public sealed record ReplayDeadOutboxResponse(
    Guid DeadMessageId,
    Guid NewOutboxId,
    DateTimeOffset ReplayedAt,
    Guid ReplayedBy);

public interface IDeadLetterStore
{
    Task<IReadOnlyList<DeadOutboxMessageDto>> ListAsync(
        Guid tenantId,
        DateTimeOffset? cursorDeadAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<DeadOutboxMessageDto?> GetAsync(
        Guid tenantId,
        Guid deadMessageId,
        CancellationToken cancellationToken = default);

    Task<ReplayDeadOutboxResponse?> ReplayAsync(
        Guid tenantId,
        Guid actorId,
        Guid deadMessageId,
        string reason,
        CancellationToken cancellationToken = default);
}
