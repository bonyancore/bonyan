using System.Reflection;
using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Construction.Persistence;

/// <summary>
/// EF Core transactional outbox writer.
/// Adds outbox rows into the active DbContext change tracker within the caller's transaction.
/// </summary>
public sealed class EfOutboxWriter : IOutbox
{
    private readonly DbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly JsonSerializerOptions _jsonOptions;

    public EfOutboxWriter(
        DbContext dbContext,
        ITenantContext tenantContext,
        IClock? clock = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _clock = clock ?? new SystemClock();
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public void Publish<TEvent>(
        TEvent evt,
        Guid? projectId = null,
        Guid? actorId = null,
        Guid? correlationId = null,
        Guid? causationId = null)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var tenantId = _tenantContext.HasTenant 
            ? _tenantContext.Current.Value 
            : throw new InvalidOperationException("Cannot publish outbox event: no ambient tenant context.");

        var envelope = EventEnvelope<TEvent>.Create(
            payload: evt,
            tenantId: tenantId,
            projectId: projectId,
            actorId: actorId,
            correlationId: correlationId,
            causationId: causationId,
            occurredAt: _clock.UtcNow);

        Publish(envelope);
    }

    public void Publish<TEvent>(EventEnvelope<TEvent> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var (aggregateType, aggregateId, aggregateVersion) = ExtractAggregateInfo(envelope.Payload, envelope);

        var message = new OutboxMessage
        {
            Id = envelope.EventId,
            TenantId = envelope.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            AggregateVersion = aggregateVersion,
            Type = envelope.Type,
            Payload = JsonSerializer.Serialize(envelope.Payload, _jsonOptions),
            OccurredAt = envelope.OccurredAt,
            Attempts = 0,
            NextAttemptAt = envelope.OccurredAt,
            LockedBy = null,
            LockedUntil = null,
            DispatchedAt = null,
            DeadAt = null
        };

        _dbContext.Set<OutboxMessage>().Add(message);
    }

    public Task PublishAsync<TEvent>(
        TEvent evt,
        Guid? projectId = null,
        Guid? actorId = null,
        Guid? correlationId = null,
        Guid? causationId = null,
        CancellationToken cancellationToken = default)
    {
        Publish(evt, projectId, actorId, correlationId, causationId);
        return Task.CompletedTask;
    }

    public Task PublishAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
    {
        Publish(envelope);
        return Task.CompletedTask;
    }

    private static (string AggregateType, Guid AggregateId, long AggregateVersion) ExtractAggregateInfo<T>(
        T payload,
        EventEnvelope<T> envelope)
    {
        var type = typeof(T);

        // 1. Aggregate Type
        string aggregateType = string.Empty;
        var aggTypeProp = type.GetProperty("AggregateType", BindingFlags.Public | BindingFlags.Instance);
        if (aggTypeProp?.GetValue(payload) is string s && !string.IsNullOrWhiteSpace(s))
        {
            aggregateType = s;
        }
        else
        {
            // Parse from event type: "module.aggregate.verb.vN" -> aggregate
            var parts = envelope.Type.Split('.');
            aggregateType = parts.Length >= 2 ? parts[1] : type.Name;
        }

        // 2. Aggregate Id
        Guid aggregateId = Guid.Empty;
        var aggIdProp = type.GetProperty("AggregateId", BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetProperty("ProjectId", BindingFlags.Public | BindingFlags.Instance);

        if (aggIdProp?.GetValue(payload) is Guid g && g != Guid.Empty)
        {
            aggregateId = g;
        }
        else if (envelope.ProjectId.HasValue && envelope.ProjectId.Value != Guid.Empty)
        {
            aggregateId = envelope.ProjectId.Value;
        }
        else
        {
            aggregateId = envelope.EventId;
        }

        // 3. Aggregate Version
        long version = 1;
        var verProp = type.GetProperty("AggregateVersion", BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetProperty("Version", BindingFlags.Public | BindingFlags.Instance);

        if (verProp != null)
        {
            var val = verProp.GetValue(payload);
            if (val is long l) version = l;
            else if (val is int i) version = i;
        }

        return (aggregateType, aggregateId, version);
    }
}
