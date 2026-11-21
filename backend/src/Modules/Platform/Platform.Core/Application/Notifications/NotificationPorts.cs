namespace Construction.Platform.Core.Application.Notifications;

public sealed record NotificationRecipient(
    Guid UserId,
    string Locale = "ar",
    string? Email = null);

public sealed record NotificationIntent(
    Guid IntentId,
    string TemplateCode,
    IReadOnlyList<NotificationRecipient> Recipients,
    IReadOnlyDictionary<string, object?> Parameters,
    string? Link = null,
    string? EntityType = null,
    Guid? EntityId = null,
    Guid? ProjectId = null,
    string Priority = "normal",
    string? DedupeKey = null);

/// <summary>
/// Producer-facing intent port. Producer modules never know provider details and never choose
/// recipients from untrusted input.
/// </summary>
public interface INotificationSender
{
    Task EnqueueAsync(NotificationIntent intent, CancellationToken cancellationToken = default);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? PlainTextBody);

public sealed record EmailSendResult(bool Accepted, string? ProviderMessageId, string? ErrorClass);

/// <summary>
/// Provider-neutral email adapter. Delivery is <b>at-least-once</b>: a crash after the provider
/// accepts a message but before the delivery row is marked sent can resend it unless the provider
/// supports idempotency keys.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Resolves a recipient's email and locale; the real implementation depends on PLT-105.</summary>
public interface INotificationRecipientResolver
{
    Task<NotificationRecipient?> ResolveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
}

public interface INotificationDispatcher
{
    Task<int> DispatchAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>Inbox and preference operations for the notification endpoints (PLT-209).</summary>
public interface INotificationReader
{
    Task<IReadOnlyList<Construction.Platform.Contracts.NotificationDto>> InboxAsync(
        Guid userId,
        bool unreadOnly,
        bool includeArchived,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<long> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Construction.Kernel.Result<Construction.Platform.Contracts.NotificationDto>> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        bool archive,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Construction.Platform.Contracts.NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Construction.Kernel.Result<Construction.Platform.Contracts.NotificationPreferenceDto>> UpsertPreferenceAsync(
        Guid userId,
        Construction.Platform.Contracts.UpdateNotificationPreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Construction.Platform.Contracts.NotificationTemplateDto>> ListTemplatesAsync(
        CancellationToken cancellationToken = default);
}
