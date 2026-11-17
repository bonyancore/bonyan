using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Construction.Messaging;
using Construction.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Default implementation of IOutboxConsumerRegistry that resolves typed
/// IEventHandler{TEvent} handlers from DI and invokes them with reconstructed EventEnvelope{TEvent}.
/// </summary>
public sealed class CompositeOutboxConsumerRegistry : IOutboxConsumerRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompositeOutboxConsumerRegistry> _logger;
    private readonly ConcurrentDictionary<string, Type> _eventTypeMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions;

    public CompositeOutboxConsumerRegistry(
        IServiceProvider serviceProvider,
        ILogger<CompositeOutboxConsumerRegistry>? logger = null,
        JsonSerializerOptions? jsonOptions = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? NullLogger<CompositeOutboxConsumerRegistry>.Instance;
        _jsonOptions = jsonOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void RegisterEventType(string eventType, Type payloadType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payloadType);

        _eventTypeMap[eventType] = payloadType;
    }

    public async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!_eventTypeMap.TryGetValue(message.Type, out var payloadType))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Outbox event type '{EventType}' (MessageId: {MessageId}) has no registered payload type. Skipping delivery.",
                    message.Type,
                    message.Id);
            }
            return;
        }

        object? payload;
        try
        {
            payload = JsonSerializer.Deserialize(message.Payload, payloadType, _jsonOptions);
        }
        catch (JsonException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(
                    ex,
                    "Failed to deserialize payload for event type '{EventType}' (MessageId: {MessageId}).",
                    message.Type,
                    message.Id);
            }
            throw;
        }

        if (payload is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Deserialized payload for event type '{EventType}' (MessageId: {MessageId}) was null. Skipping delivery.",
                    message.Type,
                    message.Id);
            }
            return;
        }

        // Invoke generic dispatcher method via reflection
        var dispatchMethod = typeof(CompositeOutboxConsumerRegistry)
            .GetMethod(nameof(DispatchToHandlersAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(payloadType);

        var task = (Task)dispatchMethod.Invoke(this, [message, payload, cancellationToken])!;
        await task.ConfigureAwait(false);
    }

    private async Task DispatchToHandlersAsync<TEvent>(
        OutboxMessage message,
        TEvent payload,
        CancellationToken cancellationToken)
    {
        var envelope = new EventEnvelope<TEvent>(
            EventId: message.Id,
            Type: message.Type,
            OccurredAt: message.OccurredAt,
            TenantId: message.TenantId,
            ProjectId: message.ProjectId,
            ActorId: message.ActorId,
            CorrelationId: message.CorrelationId,
            CausationId: message.CausationId,
            Payload: payload);

        var handlers = _serviceProvider.GetServices<IEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "No handlers registered for event '{EventType}' (MessageId: {MessageId}). Event marked as dispatched.",
                    message.Type,
                    message.Id);
            }
            return;
        }

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }
}
