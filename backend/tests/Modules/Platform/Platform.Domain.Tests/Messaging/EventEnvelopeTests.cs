using Construction.Messaging;
using Xunit;

namespace Construction.Platform.Domain.Tests.Messaging;

[EventType("test.sample.created.v1")]
public sealed record SampleCreatedEvent(Guid Id, string Name);

public sealed record UnannotatedEvent(Guid Id);

public sealed class EventEnvelopeTests
{
    [Fact]
    public void Create_WithValidParameters_SetsPropertiesCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var payload = new SampleCreatedEvent(Guid.NewGuid(), "Sample 1");
        var projectId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var envelope = EventEnvelope<SampleCreatedEvent>.Create(
            payload: payload,
            tenantId: tenantId,
            projectId: projectId,
            actorId: actorId);

        Assert.NotEqual(Guid.Empty, envelope.EventId);
        Assert.Equal("test.sample.created.v1", envelope.Type);
        Assert.Equal(tenantId, envelope.TenantId);
        Assert.Equal(projectId, envelope.ProjectId);
        Assert.Equal(actorId, envelope.ActorId);
        Assert.Equal(envelope.EventId, envelope.CorrelationId);
        Assert.Null(envelope.CausationId);
        Assert.Equal(payload, envelope.Payload);
        Assert.True(envelope.OccurredAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsArgumentException()
    {
        var payload = new SampleCreatedEvent(Guid.NewGuid(), "Sample 1");

        Assert.Throws<ArgumentException>(() =>
            EventEnvelope<SampleCreatedEvent>.Create(payload, Guid.Empty));
    }

    [Fact]
    public void Create_WithoutExplicitType_ResolvesFromEventTypeAttribute()
    {
        var tenantId = Guid.NewGuid();
        var payload = new SampleCreatedEvent(Guid.NewGuid(), "Sample");

        var envelope = EventEnvelope<SampleCreatedEvent>.Create(payload, tenantId);

        Assert.Equal("test.sample.created.v1", envelope.Type);
    }

    [Fact]
    public void Create_WithUnannotatedTypeAndNoExplicitType_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var payload = new UnannotatedEvent(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            EventEnvelope<UnannotatedEvent>.Create(payload, tenantId));
    }

    [Fact]
    public void Create_WithExplicitTypeOverride_UsesExplicitType()
    {
        var tenantId = Guid.NewGuid();
        var payload = new SampleCreatedEvent(Guid.NewGuid(), "Sample");

        var envelope = EventEnvelope<SampleCreatedEvent>.Create(
            payload, 
            tenantId, 
            type: "custom.override.verb.v2");

        Assert.Equal("custom.override.verb.v2", envelope.Type);
    }
}
