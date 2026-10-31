namespace Construction.Messaging;

/// <summary>
/// Marker interface for all integration events published and consumed across modules.
/// </summary>
public interface IIntegrationEvent
{
}

/// <summary>
/// Optional abstract record base for integration events.
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent;
