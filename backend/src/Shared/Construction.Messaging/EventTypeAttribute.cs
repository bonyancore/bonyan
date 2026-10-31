using System.Reflection;

namespace Construction.Messaging;

/// <summary>
/// Specifies the canonical event type identifier following the convention: module.aggregate.past_tense_verb.vN
/// (e.g. "cost.budget.approved.v1").
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class EventTypeAttribute : Attribute
{
    public string EventType { get; }

    public EventTypeAttribute(string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        EventType = eventType;
    }

    /// <summary>
    /// Gets the event type name from the EventTypeAttribute decorating type T.
    /// Throws InvalidOperationException if the attribute is missing.
    /// </summary>
    public static string GetEventType<T>() => GetEventType(typeof(T));

    /// <summary>
    /// Gets the event type name from the EventTypeAttribute decorating the specified type.
    /// Throws InvalidOperationException if the attribute is missing.
    /// </summary>
    public static string GetEventType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var attr = type.GetCustomAttribute<EventTypeAttribute>(inherit: false);
        return attr?.EventType 
            ?? throw new InvalidOperationException($"Type '{type.FullName}' is missing required [EventType] attribute.");
    }

    /// <summary>
    /// Tries to get the event type name from the EventTypeAttribute decorating the specified type.
    /// </summary>
    public static bool TryGetEventType(Type type, out string? eventType)
    {
        ArgumentNullException.ThrowIfNull(type);
        var attr = type.GetCustomAttribute<EventTypeAttribute>(inherit: false);
        if (attr is not null)
        {
            eventType = attr.EventType;
            return true;
        }

        eventType = null;
        return false;
    }
}
