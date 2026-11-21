using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Construction.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.7 / §5.4: file metadata, upload sessions, links and variants. Bytes live in object
    /// storage; only metadata is relational. Additive and forward-only.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261121110000_AddFiles")]
    public partial class AddFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS platform.files (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    storage_key text NOT NULL,
                    original_name text NOT NULL,
                    content_type text NOT NULL,
                    declared_content_type text NOT NULL,
                    size_bytes bigint NOT NULL DEFAULT 0,
                    sha256 text NULL,
                    status text NOT NULL DEFAULT 'pending_upload'
                        CHECK (status IN ('pending_upload', 'uploaded', 'scanning', 'available',
                                          'quarantined', 'deleted', 'purged')),
                    scan_status text NOT NULL DEFAULT 'not_scanned'
                        CHECK (scan_status IN ('not_scanned', 'pending', 'clean', 'infected', 'unavailable')),
                    scan_error text NULL,
                    uploaded_by uuid NOT NULL,
                    category text NULL,
                    visibility text NOT NULL DEFAULT 'internal'
                        CHECK (visibility IN ('internal', 'shared_external')),
                    project_id uuid NULL,
                    entity_type text NULL,
                    entity_id uuid NULL,
                    legal_hold boolean NOT NULL DEFAULT false,
                    retention_until timestamptz NULL,
                    row_version integer NOT NULL DEFAULT 1,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    updated_at timestamptz NULL,
                    deleted_at timestamptz NULL
                );

                CREATE INDEX IF NOT EXISTS ix_files_tenant_status ON platform.files (tenant_id, status);
                CREATE INDEX IF NOT EXISTS ix_files_tenant_sha ON platform.files (tenant_id, sha256);

                CREATE TABLE IF NOT EXISTS platform.upload_sessions (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    file_id uuid NOT NULL REFERENCES platform.files (id),
                    expected_size bigint NOT NULL,
                    expected_content_type text NOT NULL,
                    expected_sha256 text NULL,
                    client_ref text NULL,
                    expires_at timestamptz NOT NULL,
                    completed_at timestamptz NULL,
                    created_at timestamptz NOT NULL DEFAULT now()
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_upload_sessions_client_ref
                    ON platform.upload_sessions (tenant_id, client_ref)
                    WHERE client_ref IS NOT NULL;

                CREATE TABLE IF NOT EXISTS platform.file_links (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    file_id uuid NOT NULL REFERENCES platform.files (id),
                    entity_type text NOT NULL,
                    entity_id uuid NOT NULL,
                    link_role text NULL,
                    caption text NULL,
                    sort_order int NOT NULL DEFAULT 0,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_file_links_target UNIQUE (file_id, entity_type, entity_id)
                );

                CREATE TABLE IF NOT EXISTS platform.file_variants (
                    id uuid PRIMARY KEY,
                    tenant_id uuid NOT NULL,
                    file_id uuid NOT NULL REFERENCES platform.files (id),
                    variant text NOT NULL,
                    storage_key text NOT NULL,
                    size_bytes bigint NOT NULL DEFAULT 0,
                    created_at timestamptz NOT NULL DEFAULT now(),
                    CONSTRAINT ux_file_variants_name UNIQUE (file_id, variant)
                );
                """);

            migrationBuilder.EnableTenantRowLevelSecurity("platform", "files");
            migrationBuilder.EnableTenantRowLevelSecurity("platform", "upload_sessions");
            migrationBuilder.EnableTenantRowLevelSecurity("platform", "file_links");
            migrationBuilder.EnableTenantRowLevelSecurity("platform", "file_variants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS platform.file_variants;
                DROP TABLE IF EXISTS platform.file_links;
                DROP TABLE IF EXISTS platform.upload_sessions;
                DROP TABLE IF EXISTS platform.files;
                """);
        }
    }
}
