namespace Construction.Platform.Contracts;

public sealed record NotificationDto(
    Guid Id,
    string TemplateCode,
    string Category,
    string Title,
    string Body,
    string? Link,
    string Priority,
    bool IsRead,
    bool IsArchived,
    DateTimeOffset CreatedAt);

public sealed record NotificationPreferenceDto(
    string Category,
    string Channel,
    bool Enabled,
    string DigestMode,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo);

public sealed record UpdateNotificationPreferenceRequest(
    string Category,
    string Channel,
    bool Enabled,
    string DigestMode,
    TimeOnly? QuietHoursFrom,
    TimeOnly? QuietHoursTo);

public sealed record NotificationTemplateDto(
    Guid Id,
    string Code,
    string Locale,
    string Channel,
    int Version,
    string Status,
    string? Subject,
    string Body,
    string Category,
    bool IsMandatory);
