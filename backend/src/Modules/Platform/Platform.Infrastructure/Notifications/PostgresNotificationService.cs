using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Construction.Kernel;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts;
using Construction.Platform.Core.Application.Notifications;
using Construction.Platform.Core.Domain.Notifications;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Construction.Platform.Infrastructure.Notifications;

/// <summary>
/// PLT-209 notification service: intents are idempotent per recipient, templates are versioned and
/// locale-aware, preferences and mandatory-channel rules are applied server-side, quiet hours hold
/// non-priority email, and email delivery is at-least-once through a replaceable adapter.
/// </summary>
public sealed class PostgresNotificationService :
    INotificationSender,
    INotificationReader,
    INotificationDispatcher
{
    private static readonly string[] Channels = ["in_app", "email"];
    private static readonly string[] DigestModes = ["immediate", "daily", "off"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;
    private readonly INotificationRecipientResolver _recipients;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;

    public PostgresNotificationService(
        IConfiguration configuration,
        ITenantContext tenantContext,
        INotificationRecipientResolver recipients,
        IEmailSender email,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task EnqueueAsync(NotificationIntent intent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (string.IsNullOrWhiteSpace(intent.TemplateCode) || intent.Recipients.Count == 0)
        {
            throw new DomainRuleViolation(
                "platform.notification_template_invalid",
                "A template code and at least one recipient are required.");
        }

        var link = NotificationRules.ValidateLink(intent.Link);
        if (link.IsFailure)
        {
            throw new DomainRuleViolation(link.Error.Code, link.Error.Message);
        }

        var tenantId = CurrentTenant();
        var now = _clock.GetUtcNow();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var parameters = new Dictionary<string, object?>(intent.Parameters, StringComparer.Ordinal);
        var preferences = await LoadPreferencesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        foreach (var recipient in intent.Recipients.DistinctBy(r => r.UserId))
        {
            if (intent.DedupeKey is not null &&
                await ApplyDedupeAsync(
                    connection, transaction, recipient.UserId, intent.DedupeKey, now, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            var template = await LoadTemplateAsync(
                connection, transaction, intent.TemplateCode, recipient.Locale, "in_app", cancellationToken)
                .ConfigureAwait(false);

            if (template is null)
            {
                continue;
            }

            var missing = NotificationRules.MissingParameters(
                ExtractPlaceholders(template.Subject + " " + template.Body), parameters);

            if (missing.Count > 0)
            {
                throw new DomainRuleViolation(
                    "platform.notification_param_invalid",
                    $"Missing notification parameters: {string.Join(", ", missing)}.");
            }

            var title = NotificationRules.Render(
                template.Subject ?? intent.TemplateCode, parameters, html: false);
            var body = NotificationRules.Render(template.Body, parameters, html: false);

            var notificationId = await InsertNotificationAsync(
                connection, transaction, tenantId, intent, recipient, template, title, body,
                link.Value, parameters, cancellationToken).ConfigureAwait(false);

            if (notificationId is null)
            {
                continue;
            }

            await InsertInAppDeliveryAsync(connection, transaction, tenantId, notificationId.Value, now, cancellationToken)
                .ConfigureAwait(false);

            await InsertEmailDeliveryAsync(
                connection, transaction, tenantId, notificationId.Value, intent, template,
                recipient, preferences, now, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DispatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        var queued = new List<QueuedEmail>();
        await using (var claim = Command(connection, transaction, """
            SELECT d.id, d.notification_id, d.attempts, n.title, n.body, n.recipient_id
            FROM platform.notification_deliveries d
            JOIN platform.notifications n ON n.id = d.notification_id
            WHERE d.channel = 'email' AND d.status = 'queued'
              AND (d.next_attempt_at IS NULL OR d.next_attempt_at <= now())
            ORDER BY d.next_attempt_at NULLS FIRST
            FOR UPDATE OF d SKIP LOCKED
            LIMIT @batch;
            """))
        {
            Add(claim, "batch", batchSize);
            await using var reader = await claim.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                queued.Add(new QueuedEmail(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetGuid(5)));
            }
        }

        var sent = 0;
        foreach (var item in queued)
        {
            var recipient = await _recipients.ResolveAsync(tenantId, item.RecipientId, cancellationToken)
                .ConfigureAwait(false);

            EmailSendResult result;
            if (recipient?.Email is null)
            {
                result = new EmailSendResult(false, null, "recipient_not_found");
            }
            else
            {
                result = await _email.SendAsync(
                    new EmailMessage(
                        recipient.Email,
                        item.Title,
                        item.Body,
                        item.Body),
                    cancellationToken).ConfigureAwait(false);
            }

            if (result.Accepted)
            {
                await using var done = Command(connection, transaction, """
                    UPDATE platform.notification_deliveries
                    SET status = 'sent', attempts = attempts + 1, sent_at = now(),
                        provider_message_id = @providerId, error_class = NULL
                    WHERE id = @id;
                    """);
                Add(done, "providerId", (object?)result.ProviderMessageId ?? DBNull.Value);
                Add(done, "id", item.DeliveryId);
                await done.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                sent++;
            }
            else
            {
                var (delay, terminal) = NotificationRules.RetrySchedule(item.Attempts + 1);
                await using var failed = Command(connection, transaction, """
                    UPDATE platform.notification_deliveries
                    SET status = @status,
                        attempts = attempts + 1,
                        next_attempt_at = @nextAttempt,
                        error_class = @errorClass
                    WHERE id = @id;
                    """);
                Add(failed, "status", terminal ? "failed" : "queued");
                Add(failed, "nextAttempt", terminal ? DBNull.Value : _clock.GetUtcNow() + delay);
                Add(failed, "errorClass", (object?)result.ErrorClass ?? "send_failed");
                Add(failed, "id", item.DeliveryId);
                await failed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return sent;
    }

    public async Task<IReadOnlyList<NotificationDto>> InboxAsync(
        Guid userId,
        bool unreadOnly,
        bool includeArchived,
        DateTimeOffset? cursorCreatedAt,
        Guid? cursorId,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            SELECT id, template_code, category, title, body, link, priority, read_at, archived_at, created_at
            FROM platform.notifications
            WHERE recipient_id = @userId
              AND (@unreadOnly = false OR read_at IS NULL)
              AND (@includeArchived = true OR archived_at IS NULL)
              AND (@cursorAt IS NULL OR (created_at, id) < (@cursorAt, @cursorId))
            ORDER BY created_at DESC, id DESC
            LIMIT @limit;
            """);
        Add(command, "userId", userId);
        Add(command, "unreadOnly", unreadOnly);
        Add(command, "includeArchived", includeArchived);
        Add(command, "cursorAt", (object?)cursorCreatedAt ?? DBNull.Value);
        Add(command, "cursorId", (object?)cursorId ?? DBNull.Value);
        Add(command, "limit", pageSize);

        var items = new List<NotificationDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(ReadNotification(reader));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return items;
    }

    public async Task<long> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            SELECT COUNT(*) FROM platform.notifications
            WHERE recipient_id = @userId AND read_at IS NULL AND archived_at IS NULL;
            """);
        Add(command, "userId", userId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public async Task<Result<NotificationDto>> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        bool archive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            UPDATE platform.notifications
            SET read_at = COALESCE(read_at, now()),
                archived_at = CASE WHEN @archive THEN now() ELSE archived_at END
            WHERE id = @id AND recipient_id = @userId
            RETURNING id, template_code, category, title, body, link, priority, read_at, archived_at, created_at;
            """);
        Add(command, "id", notificationId);
        Add(command, "userId", userId);
        Add(command, "archive", archive);

        NotificationDto? updated = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                updated = ReadNotification(reader);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return updated is null
            ? Result.Failure<NotificationDto>(Error.NotFound(
                "platform.notification_not_found", "The notification was not found."))
            : Result.Success(updated);
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            UPDATE platform.notifications SET read_at = now()
            WHERE recipient_id = @userId AND read_at IS NULL;
            """);
        Add(command, "userId", userId);
        var updated = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<IReadOnlyList<NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            SELECT category, channel, enabled, digest_mode, quiet_hours_from, quiet_hours_to
            FROM platform.notification_preferences
            WHERE user_id = @userId
            ORDER BY category, channel;
            """);
        Add(command, "userId", userId);

        var items = new List<NotificationPreferenceDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new NotificationPreferenceDto(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetFieldValue<TimeOnly>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<TimeOnly>(5)));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return items;
    }

    public async Task<Result<NotificationPreferenceDto>> UpsertPreferenceAsync(
        Guid userId,
        UpdateNotificationPreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Category) ||
            !Channels.Contains(request.Channel, StringComparer.Ordinal) ||
            !DigestModes.Contains(request.DigestMode, StringComparer.Ordinal))
        {
            return Result.Failure<NotificationPreferenceDto>(Error.Validation(
                "platform.notification_preference_invalid",
                "Unknown category, channel or digest mode."));
        }

        if (request.QuietHoursFrom.HasValue != request.QuietHoursTo.HasValue)
        {
            return Result.Failure<NotificationPreferenceDto>(Error.Validation(
                "platform.notification_preference_invalid",
                "Both quiet-hours endpoints must be supplied together."));
        }

        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            INSERT INTO platform.notification_preferences
                (id, tenant_id, user_id, category, channel, enabled, digest_mode,
                 quiet_hours_from, quiet_hours_to, updated_at)
            VALUES
                (@id, @tenantId, @userId, @category, @channel, @enabled, @digestMode,
                 @quietFrom, @quietTo, now())
            ON CONFLICT (tenant_id, user_id, category, channel) DO UPDATE
            SET enabled = EXCLUDED.enabled,
                digest_mode = EXCLUDED.digest_mode,
                quiet_hours_from = EXCLUDED.quiet_hours_from,
                quiet_hours_to = EXCLUDED.quiet_hours_to,
                updated_at = now();
            """);
        Add(command, "id", Guid.CreateVersion7());
        Add(command, "tenantId", tenantId);
        Add(command, "userId", userId);
        Add(command, "category", request.Category);
        Add(command, "channel", request.Channel);
        Add(command, "enabled", request.Enabled);
        Add(command, "digestMode", request.DigestMode);
        Add(command, "quietFrom", (object?)request.QuietHoursFrom ?? DBNull.Value);
        Add(command, "quietTo", (object?)request.QuietHoursTo ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(new NotificationPreferenceDto(
            request.Category,
            request.Channel,
            request.Enabled,
            request.DigestMode,
            request.QuietHoursFrom,
            request.QuietHoursTo));
    }

    public async Task<IReadOnlyList<NotificationTemplateDto>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenant();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = Command(connection, transaction, """
            SELECT id, code, locale, channel, version, status, subject, body, category, is_mandatory
            FROM platform.notification_templates
            WHERE status <> 'retired'
            ORDER BY code, locale, channel, version DESC;
            """);

        var items = new List<NotificationTemplateDto>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add(new NotificationTemplateDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetBoolean(9)));
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return items;
    }

    private static async Task<Guid?> InsertNotificationAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid tenantId,
        NotificationIntent intent,
        NotificationRecipient recipient,
        TemplateRow template,
        string title,
        string body,
        string link,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            INSERT INTO platform.notifications
                (id, tenant_id, intent_id, recipient_id, template_code, category, title, body, link,
                 entity_type, entity_id, project_id, priority, dedupe_key, params)
            VALUES
                (@id, @tenantId, @intentId, @recipientId, @templateCode, @category, @title, @body, @link,
                 @entityType, @entityId, @projectId, @priority, @dedupeKey, @params::jsonb)
            ON CONFLICT (tenant_id, intent_id, recipient_id) DO NOTHING
            RETURNING id;
            """);
        Add(command, "id", Guid.CreateVersion7());
        Add(command, "tenantId", tenantId);
        Add(command, "intentId", intent.IntentId);
        Add(command, "recipientId", recipient.UserId);
        Add(command, "templateCode", template.Code);
        Add(command, "category", template.Category);
        Add(command, "title", title);
        Add(command, "body", body);
        Add(command, "link", string.IsNullOrEmpty(link) ? (object)DBNull.Value : link);
        Add(command, "entityType", (object?)intent.EntityType ?? DBNull.Value);
        Add(command, "entityId", (object?)intent.EntityId ?? DBNull.Value);
        Add(command, "projectId", (object?)intent.ProjectId ?? DBNull.Value);
        Add(command, "priority", intent.Priority);
        Add(command, "dedupeKey", (object?)intent.DedupeKey ?? DBNull.Value);
        Add(command, "params", JsonSerializer.Serialize(parameters, Json));

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : (Guid)value;
    }

    private static async Task InsertInAppDeliveryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid tenantId,
        Guid notificationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            INSERT INTO platform.notification_deliveries
                (id, tenant_id, notification_id, channel, status, sent_at, delivered_at)
            VALUES (@id, @tenantId, @notificationId, 'in_app', 'delivered', @now, @now)
            ON CONFLICT (notification_id, channel) DO NOTHING;
            """);
        Add(command, "id", Guid.CreateVersion7());
        Add(command, "tenantId", tenantId);
        Add(command, "notificationId", notificationId);
        Add(command, "now", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertEmailDeliveryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid tenantId,
        Guid notificationId,
        NotificationIntent intent,
        TemplateRow template,
        NotificationRecipient recipient,
        IReadOnlyDictionary<(Guid, string, string), bool> preferences,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var mandatory = NotificationRules.IsMandatory(template.Category);
        // Opt-out model: a missing preference row means the channel stays enabled.
        var preferenceEnabled = !preferences.TryGetValue(
            (recipient.UserId, template.Category, "email"), out var allowed) || allowed;
        // Policy settings never suppress a mandatory channel (BR-PLT-112).
        var enabled = NotificationRules.ChannelAllowed(mandatory, preferenceEnabled);

        var quietEnd = mandatory || intent.Priority is "high" or "urgent"
            ? null
            : NotificationRules.QuietHoursEndUtc(QuietHours.Default, now, null);

        if (!enabled)
        {
            await using var suppressed = Command(connection, transaction, """
                INSERT INTO platform.notification_deliveries
                    (id, tenant_id, notification_id, channel, status, suppression_reason)
                VALUES (@id, @tenantId, @notificationId, 'email', 'suppressed', 'preference_off')
                ON CONFLICT (notification_id, channel) DO NOTHING;
                """);
            Add(suppressed, "id", Guid.CreateVersion7());
            Add(suppressed, "tenantId", tenantId);
            Add(suppressed, "notificationId", notificationId);
            await suppressed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var queued = Command(connection, transaction, """
            INSERT INTO platform.notification_deliveries
                (id, tenant_id, notification_id, channel, status, next_attempt_at)
            VALUES (@id, @tenantId, @notificationId, 'email', 'queued', @nextAttempt)
            ON CONFLICT (notification_id, channel) DO NOTHING;
            """);
        Add(queued, "id", Guid.CreateVersion7());
        Add(queued, "tenantId", tenantId);
        Add(queued, "notificationId", notificationId);
        Add(queued, "nextAttempt", quietEnd ?? now);
        await queued.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> ApplyDedupeAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid userId,
        string dedupeKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, """
            UPDATE platform.notifications
            SET params = jsonb_set(
                params,
                '{count}',
                to_jsonb(COALESCE((params->>'count')::int, 1) + 1)),
                created_at = now()
            WHERE id = (
                SELECT id FROM platform.notifications
                WHERE recipient_id = @userId AND dedupe_key = @dedupeKey AND read_at IS NULL
                  AND created_at > @windowStart
                ORDER BY created_at DESC
                LIMIT 1)
            RETURNING id;
            """);
        Add(command, "userId", userId);
        Add(command, "dedupeKey", dedupeKey);
        Add(command, "windowStart", now - NotificationRules.DedupeWindow);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is not null and not DBNull;
    }

    private static async Task<TemplateRow?> LoadTemplateAsync(
        DbConnection connection,
        DbTransaction transaction,
        string code,
        string? locale,
        string channel,
        CancellationToken cancellationToken)
    {
        var language = string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

        await using var command = Command(connection, transaction, """
            SELECT code, locale, subject, body, category, is_mandatory
            FROM platform.notification_templates
            WHERE code = @code AND channel = @channel AND status = 'active'
              AND locale IN (@locale, 'en', 'ar')
            ORDER BY (tenant_id IS NOT NULL) DESC,
                     (locale = @locale) DESC,
                     version DESC
            LIMIT 1;
            """);
        Add(command, "code", code);
        Add(command, "channel", channel);
        Add(command, "locale", language);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new TemplateRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(5))
            : null;
    }

    private static async Task<IReadOnlyDictionary<(Guid, string, string), bool>> LoadPreferencesAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(Guid, string, string), bool>();

        await using var command = Command(connection, transaction, """
            SELECT user_id, category, channel, enabled FROM platform.notification_preferences;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result[(reader.GetGuid(0), reader.GetString(1), reader.GetString(2))] = reader.GetBoolean(3);
        }

        return result;
    }

    private static string[] ExtractPlaceholders(string template) =>
        System.Text.RegularExpressions.Regex
            .Matches(template, @"\{\{([A-Za-z0-9_]+)\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static NotificationDto ReadNotification(DbDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6),
        !reader.IsDBNull(7),
        !reader.IsDBNull(8),
        reader.GetFieldValue<DateTimeOffset>(9));

    private Guid CurrentTenant()
    {
        if (!_tenantContext.HasTenant)
        {
            throw new InvalidOperationException("A tenant context is required.");
        }

        return _tenantContext.Current.Value;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
        command.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbCommand Command(DbConnection connection, DbTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command;
    }

    private static void Add(DbCommand command, string name, object value) =>
        command.Parameters.Add(new NpgsqlParameter(name, value));

    private sealed record TemplateRow(
        string Code,
        string Locale,
        string? Subject,
        string Body,
        string Category,
        bool IsMandatory);

    private sealed record QueuedEmail(
        Guid DeliveryId,
        Guid NotificationId,
        int Attempts,
        string Title,
        string Body,
        Guid RecipientId);
}
