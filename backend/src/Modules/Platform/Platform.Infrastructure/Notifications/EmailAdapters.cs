using Construction.Platform.Core.Application.Notifications;

namespace Construction.Platform.Infrastructure.Notifications;

/// <summary>
/// Fail-closed email adapter used until an approved provider is configured. It never reports
/// success; queued deliveries retry and then end in the terminal failed state.
/// </summary>
public sealed class UnavailableEmailSender : IEmailSender
{
    public Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new EmailSendResult(false, null, "provider_unavailable"));
}

/// <summary>
/// Fail-closed recipient resolver until PLT-105 supplies users. Delivery fails with
/// <c>recipient_not_found</c> rather than inventing addresses.
/// </summary>
public sealed class UnavailableNotificationRecipientResolver : INotificationRecipientResolver
{
    public Task<NotificationRecipient?> ResolveAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<NotificationRecipient?>(null);
}

/// <summary>
/// Development/test resolver. Addresses use the reserved <c>.invalid</c> TLD and are never sent.
/// </summary>
public sealed class FakeNotificationRecipientResolver : INotificationRecipientResolver
{
    public Task<NotificationRecipient?> ResolveAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<NotificationRecipient?>(
            new NotificationRecipient(userId, "ar", $"user-{userId:N}@example.invalid"));
}
