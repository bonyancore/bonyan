namespace Construction.Messaging;

/// <summary>
/// Handler interface for consuming strongly-typed integration events.
/// </summary>
/// <typeparam name="TEvent">The event payload type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Prescribed by architecture recipe")]
public interface IEventHandler<TEvent>
{
    Task HandleAsync(EventEnvelope<TEvent> envelope, CancellationToken cancellationToken = default);
}
