using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Construction.Platform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Spec 01 §4.5 <c>platform.outbox_dead</c>: resolution lifecycle (open/replayed/discarded) with
    /// who/when/why. Additive and forward-only; existing rows are backfilled from replay columns.
    /// </summary>
    [DbContext(typeof(PlatformDbContext))]
    [Migration("20261101101500_AddOutboxDeadResolution")]
    public partial class AddOutboxDeadResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE platform.outbox_dead
                  ADD COLUMN IF NOT EXISTS resolution text NOT NULL DEFAULT 'open',
                  ADD COLUMN IF NOT EXISTS resolved_by uuid NULL,
                  ADD COLUMN IF NOT EXISTS resolved_at timestamptz NULL,
                  ADD COLUMN IF NOT EXISTS resolution_note text NULL,
                  ADD COLUMN IF NOT EXISTS error_class text NULL;

                UPDATE platform.outbox_dead
                SET resolution = 'replayed',
                    resolved_by = COALESCE(resolved_by, replayed_by),
                    resolved_at = COALESCE(resolved_at, replayed_at)
                WHERE replayed_at IS NOT NULL AND resolution = 'open';

                ALTER TABLE platform.outbox_dead
                  ADD CONSTRAINT ck_outbox_dead_resolution
                  CHECK (resolution IN ('open', 'replayed', 'discarded'));

                -- Spec 01 §4.5 / BR-PLT-63: a discarded dead letter must carry a note.
                ALTER TABLE platform.outbox_dead
                  ADD CONSTRAINT ck_outbox_dead_discard_note
                  CHECK (resolution <> 'discarded' OR resolution_note IS NOT NULL);

                CREATE INDEX IF NOT EXISTS ix_outbox_dead_tenant_resolution_dead
                  ON platform.outbox_dead (tenant_id, resolution, dead_at DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS platform.ix_outbox_dead_tenant_resolution_dead;
                ALTER TABLE platform.outbox_dead DROP CONSTRAINT IF EXISTS ck_outbox_dead_discard_note;
                ALTER TABLE platform.outbox_dead DROP CONSTRAINT IF EXISTS ck_outbox_dead_resolution;
                ALTER TABLE platform.outbox_dead
                  DROP COLUMN IF EXISTS resolution,
                  DROP COLUMN IF EXISTS resolved_by,
                  DROP COLUMN IF EXISTS resolved_at,
                  DROP COLUMN IF EXISTS resolution_note,
                  DROP COLUMN IF EXISTS error_class;
                """);
        }
    }
}
