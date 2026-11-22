using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.8: templates, notifications, deliveries and preferences. Global templates have a
    /// null tenant and are readable by every tenant; overrides are tenant-scoped. Additive.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121130000_AddNotifications")]
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.notification_templates (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NULL,
                    code text NOT NULL,
                    locale text NOT NULL CHECK (locale IN ('ar', 'en')),
                    channel text NOT NULL CHECK (channel IN ('in_app', 'email', 'push', 'whatsapp')),
                    version int NOT NULL DEFAULT 1,
                    status text NOT NULL DEFAULT 'active' CHECK (status IN ('draft', 'active', 'retired')),
                    subject text NULL,
                    body text NOT NULL,
                    category text NOT NULL,
                    is_mandatory boolean NOT NULL DEFAULT false,
                    param_schema jsonb NULL,
                    created_at timestamptz NOT NULL DEFAULT now()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_notification_templates_key
                    ON platform.notification_templates (tenant_id, code, locale, channel, version)
                    NULLS NOT DISTINCT;

                CREATE TABLE IF NOT EXISTS platform.notifications (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    intent_id uuid NOT NULL,
                    recipient_id uuid NOT NULL,
                    template_code text NOT NULL,
                    category text NOT NULL,
                    title text NOT NULL,
                    body text NOT NULL,
                    link text NULL,
                    entity_type text NULL,
                    entity_id uuid NULL,
                    project_id uuid NULL,
                    priority text NOT NULL DEFAULT 'normal',
                    dedupe_key text NULL,
                    params jsonb NOT NULL DEFAULT '{}'::jsonb,
                    read_at timestamptz NULL,
                    archived_at timestamptz NULL,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_notifications_intent_recipient UNIQUE (tenant_id, intent_id, recipient_id)
                );

                CREATE INDEX IF NOT EXISTS ix_notifications_inbox
                    ON platform.notifications (tenant_id, recipient_id, created_at DESC, id DESC);
                CREATE INDEX IF NOT EXISTS ix_notifications_dedupe
                    ON platform.notifications (tenant_id, recipient_id, dedupe_key)
                    WHERE dedupe_key IS NOT NULL AND read_at IS NULL;

                CREATE TABLE IF NOT EXISTS platform.notification_deliveries (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    notification_id uuid NOT NULL REFERENCES platform.notifications (id),
                    channel text NOT NULL,
                    status text NOT NULL DEFAULT 'queued'
                        CHECK (status IN ('queued', 'sent', 'delivered', 'failed', 'suppressed', 'expired')),
                    suppression_reason text NULL,
                    provider text NULL,
                    provider_message_id text NULL,
                    attempts int NOT NULL DEFAULT 0,
                    next_attempt_at timestamptz NULL,
                    sent_at timestamptz NULL,
                    delivered_at timestamptz NULL,
                    error_class text NULL,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_notification_deliveries_channel UNIQUE (notification_id, channel)
                );

                CREATE INDEX IF NOT EXISTS ix_notification_deliveries_queue
                    ON platform.notification_deliveries (next_attempt_at)
                    WHERE status = 'queued';

                CREATE TABLE IF NOT EXISTS platform.notification_preferences (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    user_id uuid NOT NULL,
                    category text NOT NULL,
                    channel text NOT NULL,
                    enabled boolean NOT NULL DEFAULT true,
                    digest_mode text NOT NULL DEFAULT 'immediate'
                        CHECK (digest_mode IN ('immediate', 'daily', 'off')),
                    quiet_hours_from time NULL,
                    quiet_hours_to time NULL,
                    updated_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_notification_preferences_key
                        UNIQUE (tenant_id, user_id, category, channel)
                );
                """);

            foreach (var table in new[]
                     {
                         "notification_templates", "notifications", "notification_deliveries",
                         "notification_preferences"
                     })
            {
                migrationBuilder.EnableTenantRowLevelSecurity("platform", table);
            }

            // Global templates (tenant_id IS NULL) are readable by every tenant. Permissive policies
            // are OR'ed, so this only widens SELECT on the global rows.
            migrationBuilder.Sql("""
                CREATE POLICY nt_global_read ON platform.notification_templates
                    FOR SELECT TO app USING (tenant_id IS NULL);
                """);

            SeedTemplates(migrationBuilder);
        }

        private static void SeedTemplates(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO platform.notification_templates
                    (id, tenant_id, code, locale, channel, version, status, subject, body, category, is_mandatory)
                VALUES
                    (gen_random_uuid(), NULL, 'approval.requested', 'en', 'in_app', 1, 'active',
                     'Approval required',
                     'Approval is required for {{recordType}} {{recordNumber}} ({{amount}} {{currency}}).',
                     'approval', true),
                    (gen_random_uuid(), NULL, 'approval.requested', 'ar', 'in_app', 1, 'active',
                     'مطلوب موافقة',
                     'مطلوب موافقة على {{recordType}} {{recordNumber}} ({{amount}} {{currency}}).',
                     'approval', true),
                    (gen_random_uuid(), NULL, 'approval.requested', 'en', 'email', 1, 'active',
                     'Approval required',
                     'Approval is required for {{recordType}} {{recordNumber}}. Open the platform to review it.',
                     'approval', true),
                    (gen_random_uuid(), NULL, 'approval.requested', 'ar', 'email', 1, 'active',
                     'مطلوب موافقة',
                     'مطلوب موافقة على {{recordType}} {{recordNumber}}. افتح المنصة للمراجعة.',
                     'approval', true),
                    (gen_random_uuid(), NULL, 'file.available', 'en', 'in_app', 1, 'active',
                     'File ready',
                     'The file {{fileName}} is available.',
                     'file', false),
                    (gen_random_uuid(), NULL, 'file.available', 'ar', 'in_app', 1, 'active',
                     'الملف جاهز',
                     'الملف {{fileName}} متاح.',
                     'file', false),
                    (gen_random_uuid(), NULL, 'file.quarantined', 'en', 'in_app', 1, 'active',
                     'File quarantined',
                     'The file {{fileName}} was quarantined: {{reason}}.',
                     'security', true),
                    (gen_random_uuid(), NULL, 'file.quarantined', 'ar', 'in_app', 1, 'active',
                     'تم حجز الملف',
                     'تم حجز الملف {{fileName}}: {{reason}}.',
                     'security', true)
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.notification_preferences;
                DROP TABLE IF EXISTS platform.notification_deliveries;
                DROP TABLE IF EXISTS platform.notifications;
                DROP TABLE IF EXISTS platform.notification_templates;
                """);
        }
    }
}
